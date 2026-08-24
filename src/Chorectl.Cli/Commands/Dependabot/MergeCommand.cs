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
/// Supplies <c>merge_poll_interval_seconds</c>/<c>merge_poll_timeout_seconds</c>. Defaults to a
/// fresh <see cref="ChorectlConfig"/> (15s/120s) when not injected, matching that type's own defaults.
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

    protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken) =>
        RunAsync(settings, cancellationToken);

    public async Task<int> RunAsync(Settings settings, CancellationToken cancellationToken = default)
    {
        var repos = await restClient.DiscoverReposAsync(settings.Repo);
        VerboseLog.Write(console, settings.Verbose, settings.Json, $"Discovered {repos.Count} repo(s)");

        var prs = await graphQlClient.FetchDependabotPrsAsync(repos, cancellationToken);
        VerboseLog.Write(console, settings.Verbose, settings.Json, $"Fetched {prs.Count} Dependabot PR(s)");

        if (settings.Security)
        {
            prs = prs.Where(pr => pr.IsSecurityUpdate).ToList();
        }

        var ready = prs.Where(Classifier.IsReadyToMerge).OrderBy(p => p.Repo).ThenBy(p => p.Number).ToList();

        if (ready.Count == 0)
        {
            return DependabotActionSupport.ReportNothingToDo<MergeResult>(console, settings.Json, settings.DryRun, "No Dependabot PRs are ready to merge.");
        }

        // --json can't render an interactive prompt, so it implies --yes for action commands.
        var selected = settings.Yes || settings.Json
            ? ready.Where(Classifier.DefaultSelected).ToList()
            : SelectionScreens.PromptMerge(console, ready);

        if (selected.Count == 0)
        {
            return DependabotActionSupport.ReportNothingToDo<MergeResult>(console, settings.Json, settings.DryRun, "No PRs selected. Nothing merged.");
        }

        var owners = repos.ToDictionary(r => r.Name, r => r.Owner);

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

        try
        {
            await merger.MergeAsync(owner, refetched, cancellationToken);
            return new MergeResult(refetched, MergeOutcome.Merged);
        }
        catch (MergeNotReadyException)
        {
            // The only retryable failure - covers both "Dependabot is actively rebasing" and
            // "nothing's rebased it yet", which look identical from the API (US-12/AC-12.1).
            return await PollAndRetryMergeAsync(owner, refetched, json, cancellationToken);
        }
        catch (GitHubAuthException ex)
        {
            // Waiting won't fix a permission problem - skip immediately, no poll (AC-12.2).
            return new MergeResult(refetched, MergeOutcome.Failed, $"insufficient permission to merge - {ex.Message}");
        }
        catch (GitHubRateLimitException)
        {
            // Distinct from GitHubAuthException - not a permission problem, and not the tool's
            // fault either. Backoff-and-retry is Phase 3 (US-11); for now, report it accurately.
            return new MergeResult(refetched, MergeOutcome.Failed, "rate limited by GitHub - try again shortly");
        }
        catch (Exception ex)
        {
            // Not a per-PR condition but a likely tool bug (404, 422, unrecognized response) -
            // skip immediately with the raw error surfaced rather than a generic "not ready" (AC-12.3).
            return new MergeResult(refetched, MergeOutcome.Failed, $"unexpected error, likely a tool bug - {ex.Message}");
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
            if (current.MergeStateStatus == "DIRTY")
            {
                return new MergeResult(current, MergeOutcome.Skipped, "became conflicting while waiting");
            }

            // Retry once no longer behind *and* CI is passing on the current head - not just
            // once the conflict state clears (AC-03.4).
            if (current.MergeStateStatus != "BEHIND" && current.Ci == CiStatus.Passing)
            {
                try
                {
                    await merger.MergeAsync(owner, current, cancellationToken);
                    return new MergeResult(current, MergeOutcome.Merged);
                }
                catch (MergeNotReadyException)
                {
                    // Keep polling against the same timeout.
                }
                catch (GitHubAuthException ex)
                {
                    return new MergeResult(current, MergeOutcome.Failed, $"insufficient permission to merge - {ex.Message}");
                }
                catch (GitHubRateLimitException)
                {
                    return new MergeResult(current, MergeOutcome.Failed, "rate limited by GitHub - try again shortly");
                }
                catch (Exception ex)
                {
                    return new MergeResult(current, MergeOutcome.Failed, $"unexpected error, likely a tool bug - {ex.Message}");
                }
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
        pr.MergeStateStatus == "DIRTY" ? "conflicting"
        : pr.Ci == CiStatus.Failing ? "state changed (checks now failing)"
        : pr.Ci == CiStatus.Pending ? "state changed (checks still pending)"
        : pr.Review == ReviewStatus.ReviewRequired ? "state changed (now requires review)"
        : pr.IsDraft ? "state changed (converted to draft)"
        : "state changed (no longer ready)";
}
