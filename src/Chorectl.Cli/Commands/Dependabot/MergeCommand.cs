using Chorectl.Cli.Rendering;
using Chorectl.Cli.Rendering.Dependabot;
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
public sealed class MergeCommand(
    RestClient restClient,
    GraphQlClient graphQlClient,
    IPullRequestMerger merger,
    IAnsiConsole console,
    Func<TimeSpan, CancellationToken, Task>? delay = null,
    TimeSpan? mergePollInterval = null,
    TimeSpan? mergePollTimeout = null) : AsyncCommand<MergeCommand.Settings>
{
    public sealed class Settings : ActionSettings;

    // Hardcoded until `chorectl config` (Phase 2) can supply merge_poll_interval_seconds / merge_poll_timeout_seconds.
    private static readonly TimeSpan DefaultMergePollInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan DefaultMergePollTimeout = TimeSpan.FromSeconds(120);

    private readonly Func<TimeSpan, CancellationToken, Task> delay = delay ?? Task.Delay;
    private readonly TimeSpan mergePollInterval = mergePollInterval ?? DefaultMergePollInterval;
    private readonly TimeSpan mergePollTimeout = mergePollTimeout ?? DefaultMergePollTimeout;

    protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken) =>
        RunAsync(settings.Repo, settings.DryRun, settings.Yes, settings.Json, cancellationToken);

    public async Task<int> RunAsync(
        string? repo = null,
        bool dryRun = false,
        bool yes = false,
        bool json = false,
        CancellationToken cancellationToken = default)
    {
        var repos = await restClient.DiscoverReposAsync(repo);
        var prs = await graphQlClient.FetchDependabotPrsAsync(repos, cancellationToken);
        var ready = prs.Where(Classifier.IsReadyToMerge).OrderBy(p => p.Repo).ThenBy(p => p.Number).ToList();

        if (ready.Count == 0)
        {
            return ReportNothingToDo(json, dryRun, "No Dependabot PRs are ready to merge.");
        }

        // --json can't render an interactive prompt, so it implies --yes for action commands.
        var selected = yes || json
            ? ready.Where(Classifier.DefaultSelected).ToList()
            : SelectionScreens.PromptMerge(console, ready);

        if (selected.Count == 0)
        {
            return ReportNothingToDo(json, dryRun, "No PRs selected. Nothing merged.");
        }

        var owners = repos.ToDictionary(r => r.Name, r => r.Owner);

        if (!json)
        {
            ProgressDisplay.RenderHeader(console);
        }

        var results = await MergeSelectedAsync(owners, selected, dryRun, json, cancellationToken);

        if (json)
        {
            JsonOutput.Write(console, new ActionJsonOutput<MergeResult>(dryRun, results));
        }
        else
        {
            ProgressDisplay.RenderSummary(console, results, dryRun);
        }

        return results.Any(r => r.Outcome == MergeOutcome.Failed) ? 1 : 0;
    }

    private int ReportNothingToDo(bool json, bool dryRun, string message)
    {
        if (json)
        {
            JsonOutput.Write(console, new ActionJsonOutput<MergeResult>(dryRun, []));
        }
        else
        {
            console.MarkupLine(message);
        }

        return 0;
    }

    private async Task<List<MergeResult>> MergeSelectedAsync(
        IReadOnlyDictionary<string, string> owners,
        IReadOnlyList<DependabotPr> selected,
        bool dryRun,
        bool json,
        CancellationToken cancellationToken)
    {
        var results = new List<MergeResult>();

        foreach (var group in selected.GroupBy(pr => pr.Repo))
        {
            var owner = owners[group.Key];
            if (!json)
            {
                ProgressDisplay.RenderRepoHeader(console, group.Key);
            }

            foreach (var pr in group)
            {
                var result = await MergeOneAsync(owner, pr, dryRun, json, cancellationToken);
                results.Add(result);
                if (!json)
                {
                    ProgressDisplay.RenderResult(console, result);
                }
            }
        }

        return results;
    }

    private async Task<MergeResult> MergeOneAsync(string owner, DependabotPr pr, bool dryRun, bool json, CancellationToken cancellationToken)
    {
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
        catch (Exception ex)
        {
            // Not a per-PR condition but a likely tool bug (404, 422, unrecognized response) -
            // skip immediately with the raw error surfaced rather than a generic "not ready" (AC-12.3).
            return new MergeResult(refetched, MergeOutcome.Failed, $"unexpected error, likely a tool bug - {ex.Message}");
        }
    }

    private async Task<MergeResult> PollAndRetryMergeAsync(string owner, DependabotPr pr, bool json, CancellationToken cancellationToken)
    {
        // Checked once, off the refetch that already happened before the failed merge attempt -
        // not re-checked each iteration, so a banner that appears mid-poll won't update this
        // message. Display-only, so that's an acceptable simplification.
        if (!json)
        {
            ProgressDisplay.RenderPolling(console, pr, mergePollTimeout);
        }

        var current = pr;
        var elapsed = TimeSpan.Zero;

        while (elapsed < mergePollTimeout)
        {
            await delay(mergePollInterval, cancellationToken);
            elapsed += mergePollInterval;

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
                catch (Exception ex)
                {
                    return new MergeResult(current, MergeOutcome.Failed, $"unexpected error, likely a tool bug - {ex.Message}");
                }
            }
        }

        return new MergeResult(current, MergeOutcome.Skipped, $"still not mergeable after {mergePollTimeout.TotalSeconds:0}s");
    }

    private static string DescribeNotReady(DependabotPr pr) =>
        pr.MergeStateStatus == "DIRTY" ? "conflicting"
        : pr.Ci == CiStatus.Failing ? "state changed (checks now failing)"
        : pr.Ci == CiStatus.Pending ? "state changed (checks still pending)"
        : pr.Review == ReviewStatus.ReviewRequired ? "state changed (now requires review)"
        : pr.IsDraft ? "state changed (converted to draft)"
        : "state changed (no longer ready)";
}
