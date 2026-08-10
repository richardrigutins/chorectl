using Chorectl.Cli.Rendering.Dependabot;
using Chorectl.Core.Domain.Dependabot;
using Spectre.Console.Testing;

namespace Chorectl.Cli.Tests.Rendering.Dependabot;

public class OverviewTableTests
{
    [Fact]
    public void Render_WithNoPrs_PrintsEmptyStateMessage()
    {
        var console = new TestConsole();

        OverviewTable.Render(console, []);

        Assert.Contains("No open Dependabot PRs found.", console.Output);
    }

    [Fact]
    public void Render_WithPrs_PrintsSummaryLineWithCounts()
    {
        var console = new TestConsole();
        var prs = new[]
        {
            Pr("ygolab", 42),
            Pr("ai-team-context", 17),
        };

        OverviewTable.Render(console, prs);

        Assert.Contains("2 open Dependabot PRs across 2 repos", console.Output);
    }

    [Fact]
    public void Render_WithPrs_PrintsTableHeadersAndRowData()
    {
        var console = new TestConsole();
        var pr = Pr("ygolab", 42, dependency: "firebase-tools", semverLevel: SemverLevel.Minor);

        OverviewTable.Render(console, [pr]);

        Assert.Contains("REPO", console.Output);
        Assert.Contains("DEPENDENCY", console.Output);
        Assert.Contains("BUMP", console.Output);
        Assert.Contains("CI", console.Output);
        Assert.Contains("REVIEW", console.Output);
        Assert.Contains("MERGE", console.Output);
        Assert.Contains("ygolab", console.Output);
        Assert.Contains("42", console.Output);
        Assert.Contains("firebase-tools", console.Output);
        Assert.Contains("minor", console.Output);
    }

    [Theory]
    [InlineData(CiStatus.Passing, "✔")]
    [InlineData(CiStatus.Failing, "✖")]
    [InlineData(CiStatus.Pending, "●")]
    [InlineData(CiStatus.NoChecks, "—")]
    public void Render_MapsCiStatusToSymbol(CiStatus ci, string expectedSymbol)
    {
        var console = new TestConsole();
        var pr = Pr("repo", 1, ci: ci);

        OverviewTable.Render(console, [pr]);

        Assert.Contains(expectedSymbol, console.Output);
    }

    [Theory]
    [InlineData(ReviewStatus.Approved, "✔")]
    [InlineData(ReviewStatus.ReviewRequired, "○ req'd")]
    [InlineData(ReviewStatus.NotRequired, "—")]
    public void Render_MapsReviewStatusToSymbol(ReviewStatus review, string expectedSymbol)
    {
        var console = new TestConsole();
        var pr = Pr("repo", 1, review: review);

        OverviewTable.Render(console, [pr]);

        Assert.Contains(expectedSymbol, console.Output);
    }

    [Theory]
    [InlineData("CLEAN", "✔ clean")]
    [InlineData("DIRTY", "⚠ conflicts")]
    [InlineData("BEHIND", "⚠ behind")]
    public void Render_MapsMergeStateStatusToSymbol(string mergeStateStatus, string expected)
    {
        var console = new TestConsole();
        var pr = Pr("repo", 1, mergeStateStatus: mergeStateStatus);

        OverviewTable.Render(console, [pr]);

        Assert.Contains(expected, console.Output);
    }

    [Fact]
    public void Render_WithDependencyNameContainingMarkupCharacters_DoesNotThrowAndRendersLiterally()
    {
        var console = new TestConsole();
        var pr = Pr("repo", 1, dependency: "[special]/pkg");

        OverviewTable.Render(console, [pr]);

        Assert.Contains("[special]/pkg", console.Output);
    }

    [Fact]
    public void Render_WithPrs_PrintsLegend()
    {
        var console = new TestConsole();

        OverviewTable.Render(console, [Pr("repo", 1)]);

        Assert.Contains("Legend:", console.Output);
    }

    private static DependabotPr Pr(
        string repo,
        int number,
        string dependency = "some-dependency",
        SemverLevel semverLevel = SemverLevel.Patch,
        CiStatus ci = CiStatus.Passing,
        ReviewStatus review = ReviewStatus.Approved,
        string mergeStateStatus = "CLEAN") => new()
        {
            Repo = repo,
            Number = number,
            Title = $"Bump {dependency} from 1.0.0 to 1.0.1",
            Url = $"https://github.com/octocat/{repo}/pull/{number}",
            HeadRefName = "dependabot/some-branch",
            DependencyName = dependency,
            SemverLevel = semverLevel,
            Ci = ci,
            Review = review,
            MergeStateStatus = mergeStateStatus,
        };
}
