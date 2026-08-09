using Chorectl.Cli.Rendering.Dependabot;
using Chorectl.Core.Domain.Dependabot;
using Chorectl.Core.GitHub;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Chorectl.Cli.Commands.Dependabot;

/// <summary>
/// <c>chorectl dependabot merge</c> — lets the user select ready Dependabot PRs and merges them,
/// re-verifying each PR's state immediately before merging and waiting between merges on the
/// same repo.
/// </summary>
public sealed class MergeCommand(
    RestClient restClient,
    GraphQlClient graphQlClient,
    IPullRequestMerger merger,
    IAnsiConsole console,
    Func<TimeSpan, CancellationToken, Task>? delay = null) : AsyncCommand
{
    // ponytail: hardcoded until `chorectl config` (Phase 2) can supply merge_wait_seconds.
    private static readonly TimeSpan MergeWaitBetweenSameRepoMerges = TimeSpan.FromSeconds(30);

    private readonly Func<TimeSpan, CancellationToken, Task> delay = delay ?? Task.Delay;

    protected override Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken) => RunAsync(cancellationToken);

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var repos = await restClient.DiscoverReposAsync();
        var prs = await graphQlClient.FetchDependabotPrsAsync(repos, cancellationToken);
        var ready = prs.Where(Classifier.IsReadyToMerge).OrderBy(p => p.Repo).ThenBy(p => p.Number).ToList();

        if (ready.Count == 0)
        {
            console.MarkupLine("No Dependabot PRs are ready to merge.");
            return 0;
        }

        var selected = SelectionScreens.PromptMerge(console, ready);
        if (selected.Count == 0)
        {
            console.MarkupLine("No PRs selected. Nothing merged.");
            return 0;
        }

        var owners = repos.ToDictionary(r => r.Name, r => r.Owner);

        ProgressDisplay.RenderHeader(console);
        var results = await MergeSelectedAsync(owners, selected, cancellationToken);
        ProgressDisplay.RenderSummary(console, results);

        return results.Any(r => r.Outcome == MergeOutcome.Failed) ? 1 : 0;
    }

    private async Task<List<MergeResult>> MergeSelectedAsync(
        IReadOnlyDictionary<string, string> owners,
        IReadOnlyList<DependabotPr> selected,
        CancellationToken cancellationToken)
    {
        var results = new List<MergeResult>();

        foreach (var group in selected.GroupBy(pr => pr.Repo))
        {
            var owner = owners[group.Key];
            var prsInRepo = group.ToList();
            ProgressDisplay.RenderRepoHeader(console, group.Key);

            for (var i = 0; i < prsInRepo.Count; i++)
            {
                var result = await MergeOneAsync(owner, prsInRepo[i], cancellationToken);
                results.Add(result);
                ProgressDisplay.RenderResult(console, result);

                if (result.Outcome == MergeOutcome.Merged && i < prsInRepo.Count - 1)
                {
                    ProgressDisplay.RenderWaiting(console, MergeWaitBetweenSameRepoMerges);
                    await delay(MergeWaitBetweenSameRepoMerges, cancellationToken);
                }
            }
        }

        return results;
    }

    private async Task<MergeResult> MergeOneAsync(string owner, DependabotPr pr, CancellationToken cancellationToken)
    {
        var refetched = await graphQlClient.RefetchAsync(owner, pr, cancellationToken);
        if (refetched is null)
        {
            return new MergeResult(pr, MergeOutcome.Skipped, "no longer open");
        }

        if (!Classifier.IsReadyToMerge(refetched))
        {
            return new MergeResult(refetched, MergeOutcome.Skipped, DescribeNotReady(refetched));
        }

        try
        {
            await merger.MergeAsync(owner, refetched, cancellationToken);
            return new MergeResult(refetched, MergeOutcome.Merged);
        }
        catch (Exception ex)
        {
            return new MergeResult(refetched, MergeOutcome.Failed, ex.Message);
        }
    }

    private static string DescribeNotReady(DependabotPr pr) =>
        pr.MergeStateStatus is "DIRTY" or "BEHIND" ? "state changed (now needs rebase)"
        : pr.Ci == CiStatus.Failing ? "state changed (checks now failing)"
        : pr.Ci == CiStatus.Pending ? "state changed (checks still pending)"
        : pr.Review == ReviewStatus.ReviewRequired ? "state changed (now requires review)"
        : pr.IsDraft ? "state changed (converted to draft)"
        : "state changed (no longer ready)";
}
