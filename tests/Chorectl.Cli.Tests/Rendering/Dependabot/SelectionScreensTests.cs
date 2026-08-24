using Chorectl.Cli.Rendering.Dependabot;
using Chorectl.Core.Config;
using Chorectl.Core.Domain.Dependabot;
using Spectre.Console.Testing;

namespace Chorectl.Cli.Tests.Rendering.Dependabot;

public class SelectionScreensTests
{
    [Fact]
    public void PromptMerge_WithSecurityUpdate_ShowsSecurityBadge()
    {
        var console = new TestConsole().Interactive();
        console.Input.PushKey(ConsoleKey.Enter);
        var pr = Pr(isSecurityUpdate: true);

        SelectionScreens.PromptMerge(console, [pr], new DefaultSelectConfig());

        Assert.Contains("security", console.Output);
    }

    [Fact]
    public void PromptMerge_WithGroupedUpdate_ShowsGroupedBadge()
    {
        var console = new TestConsole().Interactive();
        console.Input.PushKey(ConsoleKey.Enter);
        var pr = Pr(isGrouped: true);

        SelectionScreens.PromptMerge(console, [pr], new DefaultSelectConfig());

        Assert.Contains("grouped", console.Output);
    }

    [Fact]
    public void PromptMerge_WithSecurityUpdate_DoesNotPreSelectIt()
    {
        var console = new TestConsole().Interactive();
        console.Input.PushKey(ConsoleKey.Enter);
        var pr = Pr(isSecurityUpdate: true, semverLevel: SemverLevel.Patch);

        var selected = SelectionScreens.PromptMerge(console, [pr], new DefaultSelectConfig());

        Assert.Empty(selected);
    }

    [Fact]
    public void PromptMerge_WithMajorEnabledInConfig_PreSelectsMajorBumps()
    {
        var console = new TestConsole().Interactive();
        console.Input.PushKey(ConsoleKey.Enter);
        var pr = Pr(semverLevel: SemverLevel.Major);

        var selected = SelectionScreens.PromptMerge(console, [pr], new DefaultSelectConfig { Major = true });

        Assert.Contains(pr, selected);
    }

    [Fact]
    public void PromptMerge_WithIdenticallyDescribedPrsInDifferentRepos_KeepsBothDistinctAndPreSelected()
    {
        // Two different repos bumping the same dependency the same way (e.g. actions/checkout)
        // render identical display text - the selection screen must still tell them apart.
        var console = new TestConsole().Interactive();
        console.Input.PushKey(ConsoleKey.Enter);
        var prA = Pr(repo: "repo-a");
        var prB = Pr(repo: "repo-b");

        var selected = SelectionScreens.PromptMerge(console, [prA, prB], new DefaultSelectConfig());

        Assert.Equal(2, selected.Count);
        Assert.Contains(prA, selected);
        Assert.Contains(prB, selected);
    }

    private static DependabotPr Pr(
        string repo = "repo",
        SemverLevel semverLevel = SemverLevel.Patch,
        bool isGrouped = false,
        bool isSecurityUpdate = false) => new()
        {
            Repo = repo,
            Number = 1,
            Title = "Bump some-dependency from 1.0.0 to 1.0.1",
            Url = $"https://github.com/octocat/{repo}/pull/1",
            HeadRefName = "dependabot/some-branch",
            DependencyName = "some-dependency",
            FromVersion = "1.0.0",
            ToVersion = "1.0.1",
            SemverLevel = semverLevel,
            Ci = CiStatus.Passing,
            Review = ReviewStatus.NotRequired,
            MergeStateStatus = "CLEAN",
            IsGrouped = isGrouped,
            IsSecurityUpdate = isSecurityUpdate,
        };
}
