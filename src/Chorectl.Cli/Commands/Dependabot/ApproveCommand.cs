using Chorectl.Cli.Rendering;
using Chorectl.Cli.Rendering.Dependabot;
using Chorectl.Core.Audit;
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
    IAnsiConsole console,
    IAuditLog auditLog) : AsyncCommand<ApproveCommand.Settings>
{
    public sealed class Settings : ActionSettings;

    protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken) =>
        RunAsync(settings.Repo, settings.DryRun, settings.Yes, settings.Json, settings.Verbose, cancellationToken);

    public async Task<int> RunAsync(
        string? repo = null,
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

        var needsApproval = prs.Where(Classifier.NeedsApproval).OrderBy(p => p.Repo).ThenBy(p => p.Number).ToList();

        if (needsApproval.Count == 0)
        {
            return ReportNothingToDo(json, dryRun, "No Dependabot PRs need approval.");
        }

        // --json can't render an interactive prompt, so it implies --yes for action commands.
        var selected = yes || json
            ? needsApproval
            : SelectionScreens.PromptApprove(console, needsApproval);

        if (selected.Count == 0)
        {
            return ReportNothingToDo(json, dryRun, "No PRs selected. Nothing approved.");
        }

        var owners = repos.ToDictionary(r => r.Name, r => r.Owner);

        if (!json)
        {
            ProgressDisplay.RenderApproveHeader(console);
        }

        var results = await ApprovePrsAsync(owners, selected, dryRun, json, cancellationToken);

        if (json)
        {
            JsonOutput.Write(console, new ActionJsonOutput<ApproveResult>(dryRun, results));
        }
        else
        {
            ProgressDisplay.RenderApproveSummary(console, results, dryRun);
        }

        return results.Any(r => r.Outcome == ApproveOutcome.Failed) ? 1 : 0;
    }

    private int ReportNothingToDo(bool json, bool dryRun, string message)
    {
        if (json)
        {
            JsonOutput.Write(console, new ActionJsonOutput<ApproveResult>(dryRun, []));
        }
        else
        {
            console.MarkupLine(message);
        }

        return 0;
    }

    private async Task<List<ApproveResult>> ApprovePrsAsync(
        IReadOnlyDictionary<string, string> owners,
        IReadOnlyList<DependabotPr> selected,
        bool dryRun,
        bool json,
        CancellationToken cancellationToken)
    {
        var results = new List<ApproveResult>();

        foreach (var group in selected.GroupBy(pr => pr.Repo))
        {
            var owner = owners[group.Key];
            if (!json)
            {
                ProgressDisplay.RenderRepoHeader(console, group.Key);
            }

            foreach (var pr in group)
            {
                var result = await ApproveOneAsync(owner, pr, dryRun, cancellationToken);
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
                    ProgressDisplay.RenderApproveResult(console, result);
                }
            }
        }

        return results;
    }

    private async Task<ApproveResult> ApproveOneAsync(string owner, DependabotPr pr, bool dryRun, CancellationToken cancellationToken)
    {
        // Dry run stops here - the PR would be approved, but no review is submitted (AC-08.1).
        if (dryRun)
        {
            return new ApproveResult(pr, ApproveOutcome.Approved);
        }

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

    private static string DescribeOutcome(ApproveOutcome outcome) => outcome switch
    {
        ApproveOutcome.Approved => "approved",
        ApproveOutcome.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };
}
