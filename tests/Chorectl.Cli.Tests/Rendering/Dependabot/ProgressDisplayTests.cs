using Chorectl.Cli.Rendering.Dependabot;
using Chorectl.Core.Domain.Dependabot;
using Spectre.Console.Testing;

namespace Chorectl.Cli.Tests.Rendering.Dependabot;

public class ProgressDisplayTests
{
    [Fact]
    public async Task RunLivePollAsync_ShowsInitialTimeoutBeforeAnyTick()
    {
        var console = new TestConsole();
        var pr = Pr(1);

        await ProgressDisplay.RunLivePollAsync(console, pr, TimeSpan.FromSeconds(120), _ =>
            Task.FromResult(new BatchResult<MergeOutcome>(pr, MergeOutcome.Merged)));

        Assert.Contains("polling", console.Output);
        Assert.Contains("up to 120s", console.Output);
    }

    [Fact]
    public async Task RunLivePollAsync_OnTick_UpdatesTheDisplayedCountdown()
    {
        var console = new TestConsole();
        var pr = Pr(1);

        await ProgressDisplay.RunLivePollAsync(console, pr, TimeSpan.FromSeconds(120), onTick =>
        {
            onTick(TimeSpan.FromSeconds(45));
            return Task.FromResult(new BatchResult<MergeOutcome>(pr, MergeOutcome.Merged));
        });

        Assert.Contains("45s left", console.Output);
    }

    [Fact]
    public async Task RunLivePollAsync_WithRebaseBannerOnTheInitialPr_LabelsEveryFrameAsRebaseInProgress()
    {
        var console = new TestConsole();
        console.Profile.Width = 200;
        var pr = Pr(1, body: "Dependabot is rebasing this PR due to a merge conflict.");

        await ProgressDisplay.RunLivePollAsync(console, pr, TimeSpan.FromSeconds(60), onTick =>
        {
            onTick(TimeSpan.FromSeconds(30));
            return Task.FromResult(new BatchResult<MergeOutcome>(pr, MergeOutcome.Merged));
        });

        Assert.Contains("rebase in progress, polling", console.Output);
        Assert.Contains("30s left", console.Output);
    }

    [Fact]
    public async Task RunLivePollAsync_ReturnsWhateverThePollFunctionReturns()
    {
        var console = new TestConsole();
        var pr = Pr(1);
        var expected = new BatchResult<MergeOutcome>(pr, MergeOutcome.Skipped, "became conflicting while waiting");

        var result = await ProgressDisplay.RunLivePollAsync(console, pr, TimeSpan.FromSeconds(60), _ => Task.FromResult(expected));

        Assert.Same(expected, result);
    }

    private static DependabotPr Pr(int number, string? body = null) => new()
    {
        Repo = "sample-repo",
        Number = number,
        Title = "Bump left-pad from 1.0.0 to 1.0.1",
        Url = $"https://github.com/octocat/sample-repo/pull/{number}",
        HeadRefName = "dependabot/some-branch",
        MergeStateStatus = "CLEAN",
        Body = body,
    };
}
