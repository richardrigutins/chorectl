using Chorectl.Core.GitHub;

namespace Chorectl.Core.Tests.GitHub;

public class ProcessRunnerTests
{
    // dotnet is a safe cross-platform target here (present on every dev machine and CI runner
    // that can build this repo at all) with deterministic muxer-level behavior, independent of OS.
    [Fact]
    public void Run_OnSuccess_CapturesExitCodeAndTrimmedStandardOutput()
    {
        var runner = new ProcessRunner();

        var result = runner.Run("dotnet", "--version");

        Assert.Equal(0, result.ExitCode);
        Assert.NotEmpty(result.StandardOutput);
        Assert.Equal(result.StandardOutput.Trim(), result.StandardOutput);
        Assert.Empty(result.StandardError);
    }

    [Fact]
    public void Run_OnFailure_CapturesNonZeroExitCodeAndStandardError()
    {
        var runner = new ProcessRunner();

        var result = runner.Run("dotnet", "totally-bogus-command");

        Assert.NotEqual(0, result.ExitCode);
        Assert.NotEmpty(result.StandardOutput);
        Assert.NotEmpty(result.StandardError);
        Assert.Equal(result.StandardError.Trim(), result.StandardError);
    }
}
