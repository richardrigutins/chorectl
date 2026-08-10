using Chorectl.Cli.Infrastructure;
using Chorectl.Core.GitHub;
using Spectre.Console.Testing;

namespace Chorectl.Cli.Tests.Infrastructure;

public class CompositionRootTests
{
    [Fact]
    public void Run_WithHelpFlag_NeverCallsTheAuthenticator()
    {
        var authenticator = new FakeGitHubAuthenticator(new GitHubAuthException("gh is not authenticated"));
        var console = new TestConsole();

        var exitCode = CompositionRoot.Run(authenticator, ["--help"], console);

        Assert.False(authenticator.WasCalled);
        Assert.Equal(0, exitCode);
        Assert.Contains("USAGE", console.Output);
    }

    [Fact]
    public void Run_WithNoArgs_NeverCallsTheAuthenticator()
    {
        var authenticator = new FakeGitHubAuthenticator(new GitHubAuthException("gh is not authenticated"));
        var console = new TestConsole();

        CompositionRoot.Run(authenticator, [], console);

        Assert.False(authenticator.WasCalled);
    }

    [Fact]
    public void Run_WithARealCommand_CallsTheAuthenticator_AndPrintsItsErrorMessageOnFailure()
    {
        var authenticator = new FakeGitHubAuthenticator(new GitHubAuthException("gh is not authenticated"));
        var console = new TestConsole();

        var exitCode = CompositionRoot.Run(authenticator, ["dependabot", "list"], console);

        Assert.True(authenticator.WasCalled);
        Assert.Equal(1, exitCode);
        Assert.Contains("gh is not authenticated", console.Output);
    }
}
