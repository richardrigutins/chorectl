using Chorectl.Cli.Rendering;
using Chorectl.Cli.Rendering.Dependabot;
using Chorectl.Core.Audit;
using Chorectl.Core.Config;
using Chorectl.Core.Domain.Dependabot;
using Chorectl.Core.GitHub;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Chorectl.Cli.Commands.Dependabot;

/// <summary>
/// <c>chorectl dependabot merge</c> - lets the user select ready Dependabot PRs and merges them,
/// re-verifying each PR's state immediately before merging. A PR that's merely <c>BEHIND</c> is
/// attempted directly with no wait; only a failed merge attempt falls back to polling until the
/// PR is no longer behind and CI is passing, or the poll timeout elapses.
/// </summary>
/// <param name="config">
/// Supplies <c>merge_poll_interval_seconds</c>/<c>merge_poll_timeout_seconds</c> and
/// <c>default_select.*</c> (which bump levels/grouped PRs get pre-selected). Defaults to a fresh
/// <see cref="ChorectlConfig"/> when not injected, matching that type's own defaults.
/// </param>
public sealed class MergeCommand(
    RestClient restClient,
    GraphQlClient graphQlClient,
    IPullRequestMerger merger,
    IAnsiConsole console,
    IAuditLog auditLog,
    ChorectlConfig? config = null,
    Func<TimeSpan, CancellationToken, Task>? delay = null) : AsyncCommand<MergeCommand.Settings>
{
    public sealed class Settings : ActionSettings;

    private readonly Func<TimeSpan, CancellationToken, Task> delay = delay ?? Task.Delay;
    private readonly TimeSpan mergePollInterval = TimeSpan.FromSeconds((config ?? new ChorectlConfig()).MergePollIntervalSeconds);
    private readonly TimeSpan mergePollTimeout = TimeSpan.FromSeconds((config ?? new ChorectlConfig()).MergePollTimeoutSeconds);
    private readonly DefaultSelectConfig defaultSelect = (config ?? new ChorectlConfig()).DefaultSelect;

    protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken) =>
        RunAsync(settings, cancellationToken);

    public async Task<int> RunAsync(Settings settings, CancellationToken cancellationToken = default)
    {
        var (prs, owners) = await DependabotActionSupport.FetchCandidatesAsync(restClient, graphQlClient, console, settings, cancellationToken);

        var ready = prs.Where(Classifier.IsReadyToMerge).OrderBy(p => p.Repo).ThenBy(p => p.Number).ToList();

        if (ready.Count == 0)
        {
            return DependabotActionSupport.ReportNothingToDo<MergeResult>(console, settings.Json, settings.DryRun, "No Dependabot PRs are ready to merge.");
        }

        // --json can't render an interactive prompt, so it implies --yes for action commands.
        var selected = settings.Yes || settings.Json
            ? ready.Where(pr => Classifier.DefaultSelected(pr, defaultSelect)).ToList()
            : SelectionScreens.PromptMerge(console, ready, defaultSelect);

        if (selected.Count == 0)
        {
            return DependabotActionSupport.ReportNothingToDo<MergeResult>(console, settings.Json, settings.DryRun, "No PRs selected. Nothing merged.");
        }

        if (!settings.Json)
        {
            ProgressDisplay.RenderHeader(console);
        }

        var results = await DependabotActionSupport.ExecuteGroupedByRepoAsync(
            console,
            auditLog,
            owners,
            selected,
            settings.DryRun,
            settings.Json,
            (owner, pr, ct) => MergeOneAsync(owner, pr, settings.DryRun, settings.Json, settings.Verbose, ct),
            r => r.Pr,
            r => DescribeOutcome(r.Outcome),
            r => r.Reason,
            ProgressDisplay.RenderResult,
            cancellationToken);

        if (settings.Json)
        {
            JsonOutput.Write(console, new ActionJsonOutput<MergeResult>(settings.DryRun, results));
        }
        else
        {
            ProgressDisplay.RenderSummary(console, results, settings.DryRun);
        }

        return results.Any(r => r.Outcome == MergeOutcome.Failed) ? 1 : 0;
    }

    private async Task<MergeResult> MergeOneAsync(string owner, DependabotPr pr, bool dryRun, bool json, bool verbose, CancellationToken cancellationToken)
    {
        VerboseLog.Write(console, verbose, json, $"Refetching state for {owner}/{pr.Repo}#{pr.Number}");
        var refetched = await graphQlClient.RefetchAsync(owner, pr, cancellationToken);
        if (refetched is null)
        {
            return new MergeResult(pr, MergeOutcome.Skipped, "no longer open");
        }

        // DIRTY (an actual conflict) is the only merge-state value that disqualifies a PR here -
        // BEHIND is attempted directly, no wait up front (AC-03.2, AC-03.3).
        if (!Classifier.IsReadyToMerge(refetched))
        {
            return new MergeResult(refetched, MergeOutcome.Skipped, DescribeNotReady(refetched));
        }

        // Dry run stops here - the PR would be merged, but no mutation is issued (AC-08.1).
        if (dryRun)
        {
            return new MergeResult(refetched, MergeOutcome.Merged);
        }

        // The only retryable failure is MergeNotReadyException (US-12/AC-12.1) - waiting won't
        // fix a permission problem (AC-12.2) or a likely tool bug (404, 422, unrecognized
        // response; AC-12.3), so both of those skip immediately instead of entering the poll loop.
        var result = await TryMergeAsync(owner, refetched, cancellationToken);
        return result ?? await PollAndRetryMergeAsync(owner, refetched, json, cancellationToken);
    }

    /// <summary>
    /// Attempts <see cref="IPullRequestMerger.MergeAsync"/> and classifies the outcome. Returns
    /// <see langword="null"/> only for <see cref="MergeNotReadyException"/> - the one retryable
    /// failure - since what "retryable" means differs by call site (enter the poll loop for the
    /// first attempt; keep looping for a poll-loop retry). Every other outcome, success included,
    /// is a terminal <see cref="MergeResult"/>.
    /// </summary>
    private async Task<MergeResult?> TryMergeAsync(string owner, DependabotPr pr, CancellationToken cancellationToken)
    {
        try
        {
            await merger.MergeAsync(owner, pr, cancellationToken);
            return new MergeResult(pr, MergeOutcome.Merged);
        }
        catch (MergeNotReadyException)
        {
            return null;
        }
        catch (GitHubAuthException ex)
        {
            return new MergeResult(pr, MergeOutcome.Failed, $"insufficient permission to merge - {ex.Message}");
        }
        catch (GitHubRateLimitException)
        {
            // Distinct from GitHubAuthException - not a permission problem, and not the tool's
            // fault either. Backoff-and-retry is Phase 3 (US-11); for now, report it accurately.
            return new MergeResult(pr, MergeOutcome.Failed, "rate limited by GitHub - try again shortly");
        }
        catch (Exception ex)
        {
            return new MergeResult(pr, MergeOutcome.Failed, $"unexpected error, likely a tool bug - {ex.Message}");
        }
    }

    private Task<MergeResult> PollAndRetryMergeAsync(string owner, DependabotPr pr, bool json, CancellationToken cancellationToken) =>
        json
            ? PollLoopAsync(owner, pr, null, cancellationToken)
            : ProgressDisplay.RunLivePollAsync(console, pr, mergePollTimeout, onTick => PollLoopAsync(owner, pr, onTick, cancellationToken));

    private async Task<MergeResult> PollLoopAsync(string owner, DependabotPr pr, Action<TimeSpan>? onTick, CancellationToken cancellationToken)
    {
        var current = pr;
        var elapsed = TimeSpan.Zero;

        while (elapsed < mergePollTimeout)
        {
            await delay(mergePollInterval, cancellationToken);
            elapsed += mergePollInterval;
            onTick?.Invoke(mergePollTimeout - elapsed);

            var refetched = await graphQlClient.RefetchAsync(owner, current, cancellationToken);
            if (refetched is null)
            {
                return new MergeResult(current, MergeOutcome.Skipped, "no longer open");
            }

            current = refetched;

            // Stop polling immediately rather than running out the full timeout (AC-03.5).
            if (current.MergeStateStatus == MergeStateStatuses.Dirty)
            {
                return new MergeResult(current, MergeOutcome.Skipped, "became conflicting while waiting");
            }

            // Retry once no longer behind *and* CI is passing on the current head - not just
            // once the conflict state clears (AC-03.4).
            if (current.MergeStateStatus != MergeStateStatuses.Behind && current.Ci == CiStatus.Passing)
            {
                var result = await TryMergeAsync(owner, current, cancellationToken);
                if (result is not null)
                {
                    return result;
                }

                // null means MergeNotReadyException - keep polling against the same timeout.
            }
        }

        return new MergeResult(current, MergeOutcome.Skipped, $"still not mergeable after {mergePollTimeout.TotalSeconds:0}s");
    }

    private static string DescribeOutcome(MergeOutcome outcome) => outcome switch
    {
        MergeOutcome.Merged => "merged",
        MergeOutcome.Skipped => "skipped",
        MergeOutcome.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };

    private static string DescribeNotReady(DependabotPr pr) =>
        pr.MergeStateStatus == MergeStateStatuses.Dirty ? "conflicting"
        : pr.Ci == CiStatus.Failing ? "state changed (checks now failing)"
        : pr.Ci == CiStatus.Pending ? "state changed (checks still pending)"
        : pr.Review == ReviewStatus.ReviewRequired ? "state changed (now requires review)"
        : pr.IsDraft ? "state changed (converted to draft)"
        : "state changed (no longer ready)";
}
