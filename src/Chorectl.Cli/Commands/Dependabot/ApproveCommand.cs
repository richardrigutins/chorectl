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
/// an approving review on each, re-verifying each PR is still open immediately before submitting
/// (Dependabot can close or recreate a PR between list time and act time). Repos where review
/// isn't required at all never appear, since none of their PRs have <c>NeedsApproval</c> set.
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
        var (prs, owners, truncatedRepos) = await DependabotActionSupport.FetchCandidatesAsync(restClient, graphQlClient, console, settings, cancellationToken);

        var needsApproval = prs.Where(Classifier.NeedsApproval).OrderBy(p => p.Repo).ThenBy(p => p.Number).ToList();

        if (needsApproval.Count == 0)
        {
            return DependabotActionSupport.ReportNothingToDo<BatchResult<ApproveOutcome>>(console, settings.Json, settings.DryRun, "No Dependabot PRs need approval.", truncatedRepos);
        }

        // --json can't render an interactive prompt, so it implies --yes for action commands.
        var selected = settings.Yes || settings.Json
            ? needsApproval
            : SelectionScreens.PromptApprove(console, needsApproval);

        if (selected.Count == 0)
        {
            return DependabotActionSupport.ReportNothingToDo<BatchResult<ApproveOutcome>>(console, settings.Json, settings.DryRun, "No PRs selected. Nothing approved.", truncatedRepos);
        }

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
            JsonOutput.Write(console, new ActionJsonOutput<BatchResult<ApproveOutcome>>(settings.DryRun, results, truncatedRepos));
        }
        else
        {
            ProgressDisplay.RenderApproveSummary(console, results, settings.DryRun);
        }

        return results.Any(r => r.Outcome == ApproveOutcome.Failed) ? 1 : 0;
    }

    private async Task<BatchResult<ApproveOutcome>> ApproveOneAsync(string owner, DependabotPr pr, bool dryRun, CancellationToken cancellationToken)
    {
        // Dependabot can close or recreate a PR between list time and act time - re-verify it's
        // still the same open PR immediately before submitting a review on it.
        var refetched = await graphQlClient.RefetchAsync(owner, pr, cancellationToken);
        if (refetched is null)
        {
            return new BatchResult<ApproveOutcome>(pr, ApproveOutcome.Skipped, "no longer open");
        }

        // Dry run stops here - the PR would be approved, but no review is submitted (AC-08.1).
        if (dryRun)
        {
            return new BatchResult<ApproveOutcome>(refetched, ApproveOutcome.Approved);
        }

        try
        {
            await approver.ApproveAsync(owner, refetched, cancellationToken);
            return new BatchResult<ApproveOutcome>(refetched, ApproveOutcome.Approved);
        }
        catch (GitHubAuthException ex)
        {
            return new BatchResult<ApproveOutcome>(refetched, ApproveOutcome.Failed, $"insufficient permission to review - {ex.Message}");
        }
        catch (GitHubRateLimitException)
        {
            return new BatchResult<ApproveOutcome>(refetched, ApproveOutcome.Failed, "rate limited by GitHub - try again shortly");
        }
        catch (Exception ex)
        {
            return new BatchResult<ApproveOutcome>(refetched, ApproveOutcome.Failed, $"unexpected error, likely a tool bug - {ex.Message}");
        }
    }

    private static string DescribeOutcome(ApproveOutcome outcome) => outcome switch
    {
        ApproveOutcome.Approved => "approved",
        ApproveOutcome.Skipped => "skipped",
        ApproveOutcome.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };
}
