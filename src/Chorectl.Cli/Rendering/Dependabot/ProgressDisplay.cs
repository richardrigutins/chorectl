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

    public static void RenderResult(IAnsiConsole console, MergeResult result)
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

    public static void RenderPolling(IAnsiConsole console, DependabotPr pr, TimeSpan timeout)
    {
        var status = Classifier.HasRebaseBanner(pr) ? "rebase in progress, polling" : "polling";
        console.MarkupLine($" [grey]⏳ #{pr.Number}  {Describe(pr)}   {status} (up to {timeout.TotalSeconds:0}s)...[/]");
    }

    public static void RenderSummary(IAnsiConsole console, IReadOnlyList<MergeResult> results)
    {
        var merged = results.Count(r => r.Outcome == MergeOutcome.Merged);
        var skipped = results.Count(r => r.Outcome == MergeOutcome.Skipped);
        var failed = results.Count(r => r.Outcome == MergeOutcome.Failed);

        console.WriteLine();
        console.MarkupLine($"Done: {merged} merged, {skipped} skipped, {failed} failed");
    }

    public static void RenderRebaseHeader(IAnsiConsole console)
    {
        console.MarkupLine("Requesting rebases for selected PRs...");
        console.WriteLine();
    }

    public static void RenderRebaseResult(IAnsiConsole console, RebaseResult result)
    {
        var line = $"#{result.Pr.Number}  {Describe(result.Pr)}";
        var reason = result.Reason?.EscapeMarkup();

        console.MarkupLine(result.Outcome switch
        {
            RebaseOutcome.Requested => $" [green]✔[/] {line}   requested",
            RebaseOutcome.Failed => $" [red]✖[/] {line}   failed — {reason}",
            _ => $" {line}",
        });
    }

    public static void RenderRebaseSummary(IAnsiConsole console, IReadOnlyList<RebaseResult> results)
    {
        var requested = results.Count(r => r.Outcome == RebaseOutcome.Requested);
        var failed = results.Count(r => r.Outcome == RebaseOutcome.Failed);

        console.WriteLine();
        console.MarkupLine($"Done: {requested} requested, {failed} failed");
    }

    public static void RenderApproveHeader(IAnsiConsole console)
    {
        console.MarkupLine("Approving selected PRs...");
        console.WriteLine();
    }

    public static void RenderApproveResult(IAnsiConsole console, ApproveResult result)
    {
        var line = $"#{result.Pr.Number}  {Describe(result.Pr)}";
        var reason = result.Reason?.EscapeMarkup();

        console.MarkupLine(result.Outcome switch
        {
            ApproveOutcome.Approved => $" [green]✔[/] {line}   approved",
            ApproveOutcome.Failed => $" [red]✖[/] {line}   failed — {reason}",
            _ => $" {line}",
        });
    }

    public static void RenderApproveSummary(IAnsiConsole console, IReadOnlyList<ApproveResult> results)
    {
        var approved = results.Count(r => r.Outcome == ApproveOutcome.Approved);
        var failed = results.Count(r => r.Outcome == ApproveOutcome.Failed);

        console.WriteLine();
        console.MarkupLine($"Done: {approved} approved, {failed} failed");
    }
}
