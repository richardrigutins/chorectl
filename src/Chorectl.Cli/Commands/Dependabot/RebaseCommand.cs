using Chorectl.Cli.Rendering.Dependabot;
using Chorectl.Core.Domain.Dependabot;
using Chorectl.Core.GitHub;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Chorectl.Cli.Commands.Dependabot;

/// <summary>
/// <c>chorectl dependabot rebase</c> - lets the user select PRs needing a rebase and posts
/// <c>@dependabot rebase</c> on each. Reports "requested" without waiting for Dependabot to
/// actually complete the rebase.
/// </summary>
public sealed class RebaseCommand(
    RestClient restClient,
    GraphQlClient graphQlClient,
    IPullRequestCommenter commenter,
    IAnsiConsole console) : AsyncCommand<RebaseCommand.Settings>
{
    public sealed class Settings : RepoScopedSettings;

    private const string RebaseComment = "@dependabot rebase";

    protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken) =>
        RunAsync(settings.Repo, cancellationToken);

    public async Task<int> RunAsync(string? repo = null, CancellationToken cancellationToken = default)
    {
        var repos = await restClient.DiscoverReposAsync(repo);
        var prs = await graphQlClient.FetchDependabotPrsAsync(repos, cancellationToken);
        var needsRebase = prs.Where(Classifier.NeedsRebase).OrderBy(p => p.Repo).ThenBy(p => p.Number).ToList();

        if (needsRebase.Count == 0)
        {
            console.MarkupLine("No Dependabot PRs need a rebase.");
            return 0;
        }

        var selected = SelectionScreens.PromptRebase(console, needsRebase);
        if (selected.Count == 0)
        {
            console.MarkupLine("No PRs selected. Nothing requested.");
            return 0;
        }

        var owners = repos.ToDictionary(r => r.Name, r => r.Owner);

        ProgressDisplay.RenderRebaseHeader(console);
        var results = await RequestRebasesAsync(owners, selected, cancellationToken);
        ProgressDisplay.RenderRebaseSummary(console, results);

        return results.Any(r => r.Outcome == RebaseOutcome.Failed) ? 1 : 0;
    }

    private async Task<List<RebaseResult>> RequestRebasesAsync(
        IReadOnlyDictionary<string, string> owners,
        IReadOnlyList<DependabotPr> selected,
        CancellationToken cancellationToken)
    {
        var results = new List<RebaseResult>();

        foreach (var group in selected.GroupBy(pr => pr.Repo))
        {
            var owner = owners[group.Key];
            ProgressDisplay.RenderRepoHeader(console, group.Key);

            foreach (var pr in group)
            {
                var result = await RequestOneAsync(owner, pr, cancellationToken);
                results.Add(result);
                ProgressDisplay.RenderRebaseResult(console, result);
            }
        }

        return results;
    }

    private async Task<RebaseResult> RequestOneAsync(string owner, DependabotPr pr, CancellationToken cancellationToken)
    {
        try
        {
            await commenter.CommentAsync(owner, pr, RebaseComment, cancellationToken);
            return new RebaseResult(pr, RebaseOutcome.Requested);
        }
        catch (GitHubAuthException ex)
        {
            return new RebaseResult(pr, RebaseOutcome.Failed, $"insufficient permission to comment - {ex.Message}");
        }
        catch (Exception ex)
        {
            return new RebaseResult(pr, RebaseOutcome.Failed, $"unexpected error, likely a tool bug - {ex.Message}");
        }
    }
}
