using Chorectl.Cli.Rendering.Update;
using Chorectl.Core.Update;
using Spectre.Console.Testing;

namespace Chorectl.Cli.Tests.Rendering.Update;

public class UpdateProgressDisplayTests
{
    [Fact]
    public async Task RunAsync_ShowsCheckingForReleaseBeforeAnyProgressIsReported()
    {
        var console = new TestConsole();

        await UpdateProgressDisplay.RunAsync(console, _ => Task.FromResult(new UpdateResult(true, "1.0.0", "2.0.0")));

        Assert.Contains("Checking for the latest release", console.Output);
    }

    [Theory]
    [InlineData(UpdateStage.Downloading, "Downloading update (50%)...")]
    [InlineData(UpdateStage.Extracting, "Extracting update...")]
    [InlineData(UpdateStage.ReplacingExecutable, "Replacing executable...")]
    public async Task RunAsync_RendersTheReportedStage(UpdateStage stage, string expected)
    {
        var console = new TestConsole();

        await UpdateProgressDisplay.RunAsync(console, progress =>
        {
            progress.Report(new UpdateProgress(stage, 50, 100));
            return Task.FromResult(new UpdateResult(true, "1.0.0", "2.0.0"));
        });

        Assert.Contains(expected, console.Output);
    }

    [Fact]
    public async Task RunAsync_WhenTotalBytesIsUnknown_OmitsThePercentage()
    {
        var console = new TestConsole();

        await UpdateProgressDisplay.RunAsync(console, progress =>
        {
            progress.Report(new UpdateProgress(UpdateStage.Downloading, 50, null));
            return Task.FromResult(new UpdateResult(true, "1.0.0", "2.0.0"));
        });

        Assert.Contains("Downloading update...", console.Output);
    }

    [Fact]
    public async Task RunAsync_ReturnsWhateverTheUpdateFunctionReturns()
    {
        var console = new TestConsole();
        var expected = new UpdateResult(true, "1.0.0", "2.0.0");

        var result = await UpdateProgressDisplay.RunAsync(console, _ => Task.FromResult(expected));

        Assert.Same(expected, result);
    }
}
