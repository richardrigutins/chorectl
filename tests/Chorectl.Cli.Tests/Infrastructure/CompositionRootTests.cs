using Chorectl.Cli.Infrastructure;
using Chorectl.Core.GitHub;
using Spectre.Console.Testing;

namespace Chorectl.Cli.Tests.Infrastructure;

public class CompositionRootTests : IDisposable
{
    // The startup update check writes a throttle cache on every due check, including a failed one
    // (see UpdateChecker's own doc comment) - every RunAsync call below must pass this instead of
    // letting it default to the real UpdateChecker.DefaultCachePath, or tests would pollute the
    // developer's actual ~/.config/chorectl/update-check.json.
    private readonly string _tempDir = Directory.CreateTempSubdirectory("chorectl-tests-").FullName;

    private string UpdateCheckCachePath => Path.Combine(_tempDir, "update-check.json");

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public async Task Run_WithHelpFlag_NeverCallsTheAuthenticator()
    {
        var authenticator = new FakeGitHubAuthenticator(new GitHubAuthException("gh is not authenticated"));
        var console = new TestConsole();

        var exitCode = await CompositionRoot.RunAsync(authenticator, ["--help"], console, UpdateCheckCachePath);

        Assert.False(authenticator.WasCalled);
        Assert.Equal(0, exitCode);
        Assert.Contains("USAGE", console.Output);
    }

    [Fact]
    public async Task Run_WithNoArgs_NeverCallsTheAuthenticator()
    {
        var authenticator = new FakeGitHubAuthenticator(new GitHubAuthException("gh is not authenticated"));
        var console = new TestConsole();

        await CompositionRoot.RunAsync(authenticator, [], console, UpdateCheckCachePath);

        Assert.False(authenticator.WasCalled);
    }

    [Fact]
    public async Task Run_WithARealCommand_CallsTheAuthenticator_AndPrintsItsErrorMessageOnFailure()
    {
        var authenticator = new FakeGitHubAuthenticator(new GitHubAuthException("gh is not authenticated"));
        var console = new TestConsole();

        var exitCode = await CompositionRoot.RunAsync(authenticator, ["dependabot", "list"], console, UpdateCheckCachePath);

        Assert.True(authenticator.WasCalled);
        Assert.Equal(1, exitCode);
        Assert.Contains("gh is not authenticated", console.Output);
    }

    // `config get` never talks to GitHub on its own, so it's the cleanest seam for proving the
    // update check itself runs (or doesn't) independently of the real command's own GitHub calls.

    [Fact]
    public async Task Run_ConfigGet_TriggersTheUpdateCheck_WhichFailsSilentlyAndNeverBreaksTheRealCommand()
    {
        var authenticator = new FakeGitHubAuthenticator(new GitHubAuthException("gh is not authenticated"));
        var console = new TestConsole();

        var exitCode = await CompositionRoot.RunAsync(authenticator, ["config", "get"], console, UpdateCheckCachePath);

        Assert.True(authenticator.WasCalled);
        Assert.Equal(0, exitCode);
        Assert.Contains("merge_method: squash", console.Output);
    }

    [Fact]
    public async Task Run_ConfigGet_WithNoUpdateCheckEnvVarSet_SkipsTheUpdateCheck()
    {
        var authenticator = new FakeGitHubAuthenticator(new GitHubAuthException("gh is not authenticated"));
        var console = new TestConsole();
        Environment.SetEnvironmentVariable("CHORECTL_NO_UPDATE_CHECK", "1");
        try
        {
            await CompositionRoot.RunAsync(authenticator, ["config", "get"], console, UpdateCheckCachePath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHORECTL_NO_UPDATE_CHECK", null);
        }

        Assert.False(authenticator.WasCalled);
    }

    [Theory]
    [InlineData(new object[] { new string[] { } })]
    [InlineData(new object[] { new[] { "--help" } })]
    [InlineData(new object[] { new[] { "-h" } })]
    [InlineData(new object[] { new[] { "update" } })]
    [InlineData(new object[] { new[] { "UPDATE" } })]
    [InlineData(new object[] { new[] { "dependabot", "list", "--json" } })]
    public void ShouldCheckForUpdate_ForArgsThatShouldSuppressTheCheck_ReturnsFalse(string[] args)
    {
        Assert.False(CompositionRoot.ShouldCheckForUpdate(args));
    }

    [Theory]
    [InlineData(new object[] { new[] { "dependabot", "list" } })]
    [InlineData(new object[] { new[] { "config", "get" } })]
    public void ShouldCheckForUpdate_ForARealCommandWithoutJson_ReturnsTrue(string[] args)
    {
        Assert.True(CompositionRoot.ShouldCheckForUpdate(args));
    }
}
