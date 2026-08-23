using System.ComponentModel;
using Chorectl.Cli.Rendering;
using Chorectl.Cli.Rendering.Dependabot;
using Chorectl.Core.Audit;
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
    IAnsiConsole console,
    IAuditLog auditLog) : AsyncCommand<RebaseCommand.Settings>
{
    public sealed class Settings : ActionSettings
    {
        [CommandOption("--all")]
        [Description("Widen the candidate set to every open Dependabot PR, not just ones needing a rebase. PRs that need one stay pre-selected; the rest require explicit opt-in.")]
        public bool All { get; init; }
    }

    private const string RebaseComment = "@dependabot rebase";

    protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken) =>
        RunAsync(settings.Repo, settings.All, settings.DryRun, settings.Yes, settings.Json, settings.Verbose, cancellationToken);

    public async Task<int> RunAsync(
        string? repo = null,
        bool all = false,
        bool dryRun = false,
        bool yes = false,
        bool json = false,
        bool verbose = false,
        CancellationToken cancellationToken = default)
    {
        var repos = await restClient.DiscoverReposAsync(repo);
        VerboseLog.Write(console, verbose, json, $"Discovered {repos.Count} repo(s)");

        var prs = await graphQlClient.FetchDependabotPrsAsync(repos, cancellationToken);
        VerboseLog.Write(console, verbose, json, $"Fetched {prs.Count} Dependabot PR(s)");

        var candidates = (all ? prs : prs.Where(Classifier.NeedsRebase))
            .OrderBy(p => p.Repo).ThenBy(p => p.Number).ToList();

        if (candidates.Count == 0)
        {
            return ReportNothingToDo(json, dryRun, all ? "No open Dependabot PRs." : "No Dependabot PRs need a rebase.");
        }

        // --json can't render an interactive prompt, so it implies --yes for action commands.
        var selected = yes || json
            ? candidates.Where(pr => Classifier.NeedsRebase(pr) && !Classifier.HasRebaseBanner(pr)).ToList()
            : SelectionScreens.PromptRebase(console, candidates);

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

                // Dry run performs no actual mutation, so nothing is recorded (AC-08.1).
                if (!dryRun)
                {
                    await auditLog.RecordAsync(
                        new AuditEntry(DateTimeOffset.UtcNow, result.Pr.Repo, result.Pr.Number, DescribeOutcome(result.Outcome), result.Reason),
                        cancellationToken);
                }

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

    private static string DescribeOutcome(RebaseOutcome outcome) => outcome switch
    {
        RebaseOutcome.Requested => "rebase-requested",
        RebaseOutcome.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };
}
