using Chorectl.Cli.Rendering.Dependabot;
using Chorectl.Core.Domain.Dependabot;
using Chorectl.Core.GitHub;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Chorectl.Cli.Commands.Dependabot;

/// <summary>
/// <c>chorectl dependabot approve</c> - lets the user select PRs needing approval and submits
/// an approving review on each. Repos where review isn't required at all never appear, since
/// none of their PRs have <c>NeedsApproval</c> set.
/// </summary>
public sealed class ApproveCommand(
    RestClient restClient,
    GraphQlClient graphQlClient,
    IPullRequestApprover approver,
    IAnsiConsole console) : AsyncCommand<ApproveCommand.Settings>
{
    public sealed class Settings : RepoScopedSettings;

    protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken) =>
        RunAsync(settings.Repo, cancellationToken);

    public async Task<int> RunAsync(string? repo = null, CancellationToken cancellationToken = default)
    {
        var repos = await restClient.DiscoverReposAsync(repo);
        var prs = await graphQlClient.FetchDependabotPrsAsync(repos, cancellationToken);
        var needsApproval = prs.Where(Classifier.NeedsApproval).OrderBy(p => p.Repo).ThenBy(p => p.Number).ToList();

        if (needsApproval.Count == 0)
        {
            console.MarkupLine("No Dependabot PRs need approval.");
            return 0;
        }

        var selected = SelectionScreens.PromptApprove(console, needsApproval);
        if (selected.Count == 0)
        {
            console.MarkupLine("No PRs selected. Nothing approved.");
            return 0;
        }

        var owners = repos.ToDictionary(r => r.Name, r => r.Owner);

        ProgressDisplay.RenderApproveHeader(console);
        var results = await ApprovePrsAsync(owners, selected, cancellationToken);
        ProgressDisplay.RenderApproveSummary(console, results);

        return results.Any(r => r.Outcome == ApproveOutcome.Failed) ? 1 : 0;
    }

    private async Task<List<ApproveResult>> ApprovePrsAsync(
        IReadOnlyDictionary<string, string> owners,
        IReadOnlyList<DependabotPr> selected,
        CancellationToken cancellationToken)
    {
        var results = new List<ApproveResult>();

        foreach (var group in selected.GroupBy(pr => pr.Repo))
        {
            var owner = owners[group.Key];
            ProgressDisplay.RenderRepoHeader(console, group.Key);

            foreach (var pr in group)
            {
                var result = await ApproveOneAsync(owner, pr, cancellationToken);
                results.Add(result);
                ProgressDisplay.RenderApproveResult(console, result);
            }
        }

        return results;
    }

    private async Task<ApproveResult> ApproveOneAsync(string owner, DependabotPr pr, CancellationToken cancellationToken)
    {
        try
        {
            await approver.ApproveAsync(owner, pr, cancellationToken);
            return new ApproveResult(pr, ApproveOutcome.Approved);
        }
        catch (GitHubAuthException ex)
        {
            return new ApproveResult(pr, ApproveOutcome.Failed, $"insufficient permission to review - {ex.Message}");
        }
        catch (Exception ex)
        {
            return new ApproveResult(pr, ApproveOutcome.Failed, $"unexpected error, likely a tool bug - {ex.Message}");
        }
    }
}
