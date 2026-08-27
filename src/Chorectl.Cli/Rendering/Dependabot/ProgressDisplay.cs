using Chorectl.Core.Domain.Dependabot;
using Spectre.Console;

namespace Chorectl.Cli.Rendering.Dependabot;

/// <summary>
/// Renders merge progress and the final summary for <c>chorectl dependabot merge</c>.
/// </summary>
public static class ProgressDisplay
{
    public static void RenderHeader(IAnsiConsole console)
    {
        console.MarkupLine("Merging selected PRs...");
        console.WriteLine();
    }

    public static void RenderRepoHeader(IAnsiConsole console, string repo) => console.MarkupLine(repo.EscapeMarkup());

    public static void RenderResult(IAnsiConsole console, BatchResult<MergeOutcome> result)
    {
        var line = $"#{result.Pr.Number}  {Describe(result.Pr)}";
        var reason = result.Reason?.EscapeMarkup();

        console.MarkupLine(result.Outcome switch
        {
            MergeOutcome.Merged => $" [green]✔[/] {line}   merged",
            MergeOutcome.Skipped => $" [yellow]✖[/] {line}   skipped — {reason}",
            MergeOutcome.Failed => $" [red]✖[/] {line}   failed — {reason}",
            _ => $" {line}",
        });
    }

    private static string Describe(DependabotPr pr) =>
        (pr.DependencyName is not null && pr.FromVersion is not null && pr.ToVersion is not null
            ? $"{pr.DependencyName}  {pr.FromVersion} -> {pr.ToVersion}"
            : pr.Title).EscapeMarkup();

    /// <summary>
    /// Runs <paramref name="poll"/> under a live-updating Spectre.Console status display, showing
    /// a per-PR countdown of the remaining poll time. Whether the banner label applies is decided
    /// once from <paramref name="pr"/>'s state going into the poll - not re-checked on every tick,
    /// so a banner that appears or disappears mid-poll won't update the label. Display-only, so
    /// that's an acceptable simplification.
    /// </summary>
    public static Task<BatchResult<MergeOutcome>> RunLivePollAsync(
        IAnsiConsole console,
        DependabotPr pr,
        TimeSpan timeout,
        Func<Action<TimeSpan>, Task<BatchResult<MergeOutcome>>> poll)
    {
        var label = Classifier.HasRebaseBanner(pr) ? "rebase in progress, polling" : "polling";
        var description = Describe(pr);

        string Frame(string suffix) => $" [grey]⏳ #{pr.Number}  {description}   {label} {suffix}[/]";

        return console.Status().StartAsync(
            Frame($"(up to {timeout.TotalSeconds:0}s)..."),
            ctx => poll(remaining => ctx.Status(Frame($"— {Math.Max(0, remaining.TotalSeconds):0}s left"))));
    }

    public static void RenderSummary(IAnsiConsole console, IReadOnlyList<BatchResult<MergeOutcome>> results, bool dryRun = false)
    {
        var merged = results.Count(r => r.Outcome == MergeOutcome.Merged);
        var skipped = results.Count(r => r.Outcome == MergeOutcome.Skipped);
        var failed = results.Count(r => r.Outcome == MergeOutcome.Failed);

        console.WriteLine();
        console.MarkupLine($"Done: {merged} merged, {skipped} skipped, {failed} failed");
        RenderDryRunNote(console, dryRun);
    }

    private static void RenderDryRunNote(IAnsiConsole console, bool dryRun)
    {
        if (dryRun)
        {
            console.MarkupLine("[grey]dry run — no changes made[/]");
        }
    }

    public static void RenderRebaseHeader(IAnsiConsole console)
    {
        console.MarkupLine("Requesting rebases for selected PRs...");
        console.WriteLine();
    }

    public static void RenderRebaseResult(IAnsiConsole console, BatchResult<RebaseOutcome> result)
    {
        var line = $"#{result.Pr.Number}  {Describe(result.Pr)}";
        var reason = result.Reason?.EscapeMarkup();

        console.MarkupLine(result.Outcome switch
        {
            RebaseOutcome.Requested => $" [green]✔[/] {line}   requested",
            RebaseOutcome.Skipped => $" [yellow]✖[/] {line}   skipped — {reason}",
            RebaseOutcome.Failed => $" [red]✖[/] {line}   failed — {reason}",
            _ => $" {line}",
        });
    }

    public static void RenderRebaseSummary(IAnsiConsole console, IReadOnlyList<BatchResult<RebaseOutcome>> results, bool dryRun = false)
    {
        var requested = results.Count(r => r.Outcome == RebaseOutcome.Requested);
        var skipped = results.Count(r => r.Outcome == RebaseOutcome.Skipped);
        var failed = results.Count(r => r.Outcome == RebaseOutcome.Failed);

        console.WriteLine();
        console.MarkupLine($"Done: {requested} requested, {skipped} skipped, {failed} failed");
        RenderDryRunNote(console, dryRun);
    }

    public static void RenderApproveHeader(IAnsiConsole console)
    {
        console.MarkupLine("Approving selected PRs...");
        console.WriteLine();
    }

    public static void RenderApproveResult(IAnsiConsole console, BatchResult<ApproveOutcome> result)
    {
        var line = $"#{result.Pr.Number}  {Describe(result.Pr)}";
        var reason = result.Reason?.EscapeMarkup();

        console.MarkupLine(result.Outcome switch
        {
            ApproveOutcome.Approved => $" [green]✔[/] {line}   approved",
            ApproveOutcome.Skipped => $" [yellow]✖[/] {line}   skipped — {reason}",
            ApproveOutcome.Failed => $" [red]✖[/] {line}   failed — {reason}",
            _ => $" {line}",
        });
    }

    public static void RenderApproveSummary(IAnsiConsole console, IReadOnlyList<BatchResult<ApproveOutcome>> results, bool dryRun = false)
    {
        var approved = results.Count(r => r.Outcome == ApproveOutcome.Approved);
        var skipped = results.Count(r => r.Outcome == ApproveOutcome.Skipped);
        var failed = results.Count(r => r.Outcome == ApproveOutcome.Failed);

        console.WriteLine();
        console.MarkupLine($"Done: {approved} approved, {skipped} skipped, {failed} failed");
        RenderDryRunNote(console, dryRun);
    }
}
