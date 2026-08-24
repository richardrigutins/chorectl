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

        var needsApproval = prs.Where(Classifier.NeedsApproval).OrderBy(p => p.Repo).ThenBy(p => p.Number).ToList();

        if (needsApproval.Count == 0)
        {
            return DependabotActionSupport.ReportNothingToDo<ApproveResult>(console, settings.Json, settings.DryRun, "No Dependabot PRs need approval.");
        }

        // --json can't render an interactive prompt, so it implies --yes for action commands.
        var selected = settings.Yes || settings.Json
            ? needsApproval
            : SelectionScreens.PromptApprove(console, needsApproval);

        if (selected.Count == 0)
        {
            return DependabotActionSupport.ReportNothingToDo<ApproveResult>(console, settings.Json, settings.DryRun, "No PRs selected. Nothing approved.");
        }

        var owners = repos.ToDictionary(r => r.Name, r => r.Owner);

        if (!settings.Json)
        {
            ProgressDisplay.RenderApproveHeader(console);
        }

        var results = await DependabotActionSupport.ExecuteGroupedByRepoAsync(
            console,
            auditLog,
            owners,
            selected,
            settings.DryRun,
            settings.Json,
            (owner, pr, ct) => ApproveOneAsync(owner, pr, settings.DryRun, ct),
            r => r.Pr,
            r => DescribeOutcome(r.Outcome),
            r => r.Reason,
            ProgressDisplay.RenderApproveResult,
            cancellationToken);

        if (settings.Json)
        {
            JsonOutput.Write(console, new ActionJsonOutput<ApproveResult>(settings.DryRun, results));
        }
        else
        {
            ProgressDisplay.RenderApproveSummary(console, results, settings.DryRun);
        }

        return results.Any(r => r.Outcome == ApproveOutcome.Failed) ? 1 : 0;
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
        catch (GitHubRateLimitException)
        {
            return new ApproveResult(pr, ApproveOutcome.Failed, "rate limited by GitHub - try again shortly");
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
