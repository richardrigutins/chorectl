using Chorectl.Cli.Rendering;
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
    public sealed class Settings : ActionSettings;

    private const string RebaseComment = "@dependabot rebase";

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
        var needsRebase = prs.Where(Classifier.NeedsRebase).OrderBy(p => p.Repo).ThenBy(p => p.Number).ToList();

        if (needsRebase.Count == 0)
        {
            return ReportNothingToDo(json, dryRun, "No Dependabot PRs need a rebase.");
        }

        // --json can't render an interactive prompt, so it implies --yes for action commands.
        var selected = yes || json
            ? needsRebase.Where(pr => !Classifier.HasRebaseBanner(pr)).ToList()
            : SelectionScreens.PromptRebase(console, needsRebase);

        if (selected.Count == 0)
        {
            return ReportNothingToDo(json, dryRun, "No PRs selected. Nothing requested.");
        }

        var owners = repos.ToDictionary(r => r.Name, r => r.Owner);

        if (!json)
        {
            ProgressDisplay.RenderRebaseHeader(console);
        }

        var results = await RequestRebasesAsync(owners, selected, dryRun, json, cancellationToken);

        if (json)
        {
            JsonOutput.Write(console, new ActionJsonOutput<RebaseResult>(dryRun, results));
        }
        else
        {
            ProgressDisplay.RenderRebaseSummary(console, results, dryRun);
        }

        return results.Any(r => r.Outcome == RebaseOutcome.Failed) ? 1 : 0;
    }

    private int ReportNothingToDo(bool json, bool dryRun, string message)
    {
        if (json)
        {
            JsonOutput.Write(console, new ActionJsonOutput<RebaseResult>(dryRun, []));
        }
        else
        {
            console.MarkupLine(message);
        }

        return 0;
    }

    private async Task<List<RebaseResult>> RequestRebasesAsync(
        IReadOnlyDictionary<string, string> owners,
        IReadOnlyList<DependabotPr> selected,
        bool dryRun,
        bool json,
        CancellationToken cancellationToken)
    {
        var results = new List<RebaseResult>();

        foreach (var group in selected.GroupBy(pr => pr.Repo))
        {
            var owner = owners[group.Key];
            if (!json)
            {
                ProgressDisplay.RenderRepoHeader(console, group.Key);
            }

            foreach (var pr in group)
            {
                var result = await RequestOneAsync(owner, pr, dryRun, cancellationToken);
                results.Add(result);
                if (!json)
                {
                    ProgressDisplay.RenderRebaseResult(console, result);
                }
            }
        }

        return results;
    }

    private async Task<RebaseResult> RequestOneAsync(string owner, DependabotPr pr, bool dryRun, CancellationToken cancellationToken)
    {
        // Dry run stops here - the rebase would be requested, but no comment is posted (AC-08.1).
        if (dryRun)
        {
            return new RebaseResult(pr, RebaseOutcome.Requested);
        }

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
