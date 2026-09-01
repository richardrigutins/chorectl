using System.ComponentModel;
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
/// <c>chorectl dependabot rebase</c> - lets the user select PRs needing a rebase and posts
/// <c>@dependabot rebase</c> on each, re-verifying each PR is still open immediately before
/// commenting (Dependabot can close or recreate a PR between list time and act time). Reports
/// "requested" without waiting for Dependabot to actually complete the rebase.
/// </summary>
/// <param name="config">
/// Supplies <c>max_backoff_seconds</c> (US-11). Defaults to a fresh <see cref="ChorectlConfig"/>
/// when not injected, matching that type's own defaults.
/// </param>
public sealed class RebaseCommand(
    RestClient restClient,
    GraphQlClient graphQlClient,
    IPullRequestCommenter commenter,
    IAnsiConsole console,
    IAuditLog auditLog,
    ChorectlConfig? config = null,
    Func<TimeSpan, CancellationToken, Task>? delay = null) : AsyncCommand<RebaseCommand.Settings>
{
    public sealed class Settings : ActionSettings
    {
        [CommandOption("--all")]
        [Description("Widen the candidate set to every open Dependabot PR, not just ones needing a rebase. PRs that need one stay pre-selected; the rest require explicit opt-in.")]
        public bool All { get; init; }
    }

    private const string RebaseComment = "@dependabot rebase";

    private static readonly ChorectlConfig DefaultConfig = new();

    private readonly Func<TimeSpan, CancellationToken, Task> delay = delay ?? Task.Delay;
    private readonly int maxBackoffSeconds = (config ?? DefaultConfig).MaxBackoffSeconds;

    protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken) =>
        RunAsync(settings, cancellationToken);

    public async Task<int> RunAsync(Settings settings, CancellationToken cancellationToken = default)
    {
        graphQlClient.OnRateLimitWaiting = settings.Json ? null : wait => ProgressDisplay.RenderRateLimitWait(console, wait);

        var (prs, owners, truncatedRepos) = await DependabotActionSupport.FetchCandidatesAsync(restClient, graphQlClient, console, settings, cancellationToken);

        var candidates = (settings.All ? prs : prs.Where(Classifier.NeedsRebase))
            .OrderBy(p => p.Repo).ThenBy(p => p.Number).ToList();

        if (candidates.Count == 0)
        {
            return DependabotActionSupport.ReportNothingToDo<BatchResult<RebaseOutcome>>(
                console, settings.Json, settings.DryRun, settings.All ? "No open Dependabot PRs." : "No Dependabot PRs need a rebase.", truncatedRepos);
        }

        // --json can't render an interactive prompt, so it implies --yes for action commands.
        var selected = settings.Yes || settings.Json
            ? candidates.Where(pr => Classifier.NeedsRebase(pr) && !Classifier.HasRebaseBanner(pr)).ToList()
            : SelectionScreens.PromptRebase(console, candidates);

        if (selected.Count == 0)
        {
            return DependabotActionSupport.ReportNothingToDo<BatchResult<RebaseOutcome>>(console, settings.Json, settings.DryRun, "No PRs selected. Nothing requested.", truncatedRepos);
        }

        if (!settings.Json)
        {
            ProgressDisplay.RenderRebaseHeader(console);
        }

        var results = await DependabotActionSupport.ExecuteGroupedByRepoAsync(
            console,
            auditLog,
            owners,
            selected,
            settings.DryRun,
            settings.Json,
            (owner, pr, ct) => RequestOneAsync(owner, pr, settings.DryRun, settings.Json, ct),
            r => r.Pr,
            r => DescribeOutcome(r.Outcome),
            r => r.Reason,
            ProgressDisplay.RenderRebaseResult,
            pr => new BatchResult<RebaseOutcome>(pr, RebaseOutcome.Failed, "rate limit did not clear - batch stopped"),
            cancellationToken);

        if (settings.Json)
        {
            JsonOutput.Write(console, new ActionJsonOutput<BatchResult<RebaseOutcome>>(settings.DryRun, results, truncatedRepos));
        }
        else
        {
            ProgressDisplay.RenderRebaseSummary(console, results, settings.DryRun);
        }

        return results.Any(r => r.Outcome == RebaseOutcome.Failed) ? 1 : 0;
    }

    private async Task<BatchResult<RebaseOutcome>> RequestOneAsync(string owner, DependabotPr pr, bool dryRun, bool json, CancellationToken cancellationToken)
    {
        // Dependabot can close or recreate a PR between list time and act time - re-verify it's
        // still the same open PR immediately before commenting on it.
        var refetched = await graphQlClient.RefetchAsync(owner, pr, cancellationToken);
        if (refetched is null)
        {
            return new BatchResult<RebaseOutcome>(pr, RebaseOutcome.Skipped, "no longer open");
        }

        // Dry run stops here - the rebase would be requested, but no comment is posted (AC-08.1).
        if (dryRun)
        {
            return new BatchResult<RebaseOutcome>(refetched, RebaseOutcome.Requested);
        }

        try
        {
            await RateLimitBackoff.RunAsync(
                () => commenter.CommentAsync(owner, refetched, RebaseComment, cancellationToken),
                maxBackoffSeconds,
                delay,
                json ? null : wait => ProgressDisplay.RenderRateLimitWait(console, wait),
                cancellationToken);
            return new BatchResult<RebaseOutcome>(refetched, RebaseOutcome.Requested);
        }
        catch (GitHubAuthException ex)
        {
            return new BatchResult<RebaseOutcome>(refetched, RebaseOutcome.Failed, $"insufficient permission to comment - {ex.Message}");
        }
        catch (Exception ex) when (ex is not RateLimitBackoffExhaustedException)
        {
            return new BatchResult<RebaseOutcome>(refetched, RebaseOutcome.Failed, $"unexpected error, likely a tool bug - {ex.Message}");
        }
    }

    private static string DescribeOutcome(RebaseOutcome outcome) => outcome switch
    {
        RebaseOutcome.Requested => "rebase-requested",
        RebaseOutcome.Skipped => "skipped",
        RebaseOutcome.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };
}
