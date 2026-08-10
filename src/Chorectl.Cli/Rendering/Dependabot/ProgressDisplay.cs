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

    public static void RenderWaiting(IAnsiConsole console, TimeSpan wait) =>
        console.MarkupLine($" [grey]⏳ waiting {wait.TotalSeconds:0}s before next merge on this repo...[/]");

    public static void RenderSummary(IAnsiConsole console, IReadOnlyList<MergeResult> results)
    {
        var merged = results.Count(r => r.Outcome == MergeOutcome.Merged);
        var skipped = results.Count(r => r.Outcome == MergeOutcome.Skipped);
        var failed = results.Count(r => r.Outcome == MergeOutcome.Failed);

        console.WriteLine();
        console.MarkupLine($"Done: {merged} merged, {skipped} skipped, {failed} failed");
    }
}
