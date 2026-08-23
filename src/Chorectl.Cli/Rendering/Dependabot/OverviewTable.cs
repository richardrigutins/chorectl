using Chorectl.Core.Domain.Dependabot;
using Spectre.Console;

namespace Chorectl.Cli.Rendering.Dependabot;

/// <summary>
/// Renders the non-interactive overview table for <c>chorectl dependabot list</c>.
/// </summary>
public static class OverviewTable
{
    public static void Render(IAnsiConsole console, IReadOnlyList<DependabotPr> prs)
    {
        if (prs.Count == 0)
        {
            console.MarkupLine("No open Dependabot PRs found.");
            return;
        }

        var repoCount = prs.Select(p => p.Repo).Distinct().Count();
        console.MarkupLine(
            $"chorectl dependabot list [grey]·[/] {prs.Count} open Dependabot {Pluralize("PR", prs.Count)} across {repoCount} {Pluralize("repo", repoCount)}");
        console.WriteLine();

        var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
        table.AddColumn("[bold]REPO[/]");
        table.AddColumn("[bold]#[/]");
        table.AddColumn("[bold]DEPENDENCY[/]");
        table.AddColumn("[bold]BUMP[/]");
        table.AddColumn("[bold]CI[/]");
        table.AddColumn("[bold]REVIEW[/]");
        table.AddColumn("[bold]MERGE[/]");

        foreach (var pr in prs.OrderBy(p => p.Repo).ThenBy(p => p.Number))
        {
            table.AddRow(
                $"[bold]{pr.Repo.EscapeMarkup()}[/]",
                pr.Number.ToString(),
                (pr.DependencyName ?? "-").EscapeMarkup() + Badges.Markup(pr),
                BumpStyle.Markup(pr.SemverLevel),
                CiSymbol(pr.Ci),
                ReviewSymbol(pr.Review),
                MergeSymbol(pr.MergeStateStatus));
        }

        console.Write(table);
        console.WriteLine();
        console.MarkupLine("Legend: [green]✔[/] pass/approved/clean  [red]✖[/] fail  [yellow]●[/] pending  ○ required  ⚠ needs rebase  — not required");
    }

    private static string CiSymbol(CiStatus ci) => ci switch
    {
        CiStatus.Passing => "[green]✔[/]",
        CiStatus.Failing => "[red]✖[/]",
        CiStatus.Pending => "[yellow]●[/]",
        _ => "—",
    };

    private static string ReviewSymbol(ReviewStatus review) => review switch
    {
        ReviewStatus.Approved => "[green]✔[/]",
        ReviewStatus.ReviewRequired => "○ req'd",
        _ => "—",
    };

    private static string MergeSymbol(string mergeStateStatus) => mergeStateStatus switch
    {
        "CLEAN" => "[green]✔ clean[/]",
        "DIRTY" => "[yellow]⚠ conflicts[/]",
        "BEHIND" => "[yellow]⚠ behind[/]",
        "BLOCKED" => "[yellow]⚠ blocked[/]",
        "UNSTABLE" => "[yellow]⚠ unstable[/]",
        _ => $"○ {mergeStateStatus.ToLowerInvariant()}",
    };

    private static string Pluralize(string word, int count) => count == 1 ? word : word + "s";
}
