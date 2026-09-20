using Chorectl.Core.GitHub;
using Chorectl.Core.Process;
using Chorectl.Core.Tests.Process;

namespace Chorectl.Core.Tests.GitHub;

public class GhCliAuthenticatorTests
{
    [Fact]
    public void GetToken_WhenGhNotOnPath_ThrowsWithInstallInstructions()
    {
        var runner = new FakeProcessRunner { ThrowGhNotFound = true };
        var auth = new GhCliAuthenticator(runner);

        var ex = Assert.Throws<GitHubAuthException>(auth.GetToken);
        Assert.Contains("https://cli.github.com", ex.Message);
    }

    [Fact]
    public void GetToken_WhenGhVersionBelowMinimum_ThrowsWithVersionMessage()
    {
        var runner = new FakeProcessRunner();
        runner.SetResponse("--version", new ProcessResult(0, "gh version 2.0.0 (2021-01-01)", ""));
        var auth = new GhCliAuthenticator(runner);

        var ex = Assert.Throws<GitHubAuthException>(auth.GetToken);
        Assert.Contains("2.5.0", ex.Message);
    }

    [Fact]
    public void GetToken_WhenNotAuthenticated_ThrowsWithLoginMessage()
    {
        var runner = new FakeProcessRunner();
        runner.SetResponse("--version", new ProcessResult(0, "gh version 2.40.1 (2023-12-13)", ""));
        runner.SetResponse("auth status", new ProcessResult(1, "", "You are not logged into any GitHub hosts."));
        var auth = new GhCliAuthenticator(runner);

        var ex = Assert.Throws<GitHubAuthException>(auth.GetToken);
        Assert.Contains("gh auth login", ex.Message);
    }

    [Fact]
    public void GetToken_WhenInstalledAndAuthenticated_ReturnsToken()
    {
        var runner = new FakeProcessRunner();
        runner.SetResponse("--version", new ProcessResult(0, "gh version 2.40.1 (2023-12-13)", ""));
        runner.SetResponse("auth status", new ProcessResult(0, "", "Logged in to github.com as octocat"));
        runner.SetResponse("auth token", new ProcessResult(0, "gho_faketoken123", ""));
        var auth = new GhCliAuthenticator(runner);

        var token = auth.GetToken();

        Assert.Equal("gho_faketoken123", token);
    }

    // FakeProcessRunner throws if GetToken ends up running arguments nobody registered a response
    // for, so a passing test here already proves exactly which arguments were used - no need to
    // separately record and assert on the commands run.

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("github.com")]
    public void GetToken_ForTheDefaultHost_NeverPassesHostname(string? host)
    {
        var runner = new FakeProcessRunner();
        runner.SetResponse("--version", new ProcessResult(0, "gh version 2.40.1 (2023-12-13)", ""));
        runner.SetResponse("auth status", new ProcessResult(0, "", "Logged in to github.com as octocat"));
        runner.SetResponse("auth token", new ProcessResult(0, "gho_faketoken123", ""));
        var auth = new GhCliAuthenticator(runner, () => host);

        var token = auth.GetToken();

        Assert.Equal("gho_faketoken123", token);
    }

    [Fact]
    public void GetToken_ForAnEnterpriseHost_PassesHostnameToAuthStatusAndAuthToken()
    {
        var runner = new FakeProcessRunner();
        runner.SetResponse("--version", new ProcessResult(0, "gh version 2.40.1 (2023-12-13)", ""));
        runner.SetResponse("auth status --hostname github.mycompany.com", new ProcessResult(0, "", "Logged in to github.mycompany.com as octocat"));
        runner.SetResponse("auth token --hostname github.mycompany.com", new ProcessResult(0, "ghe_faketoken123", ""));
        var auth = new GhCliAuthenticator(runner, () => "github.mycompany.com");

        var token = auth.GetToken();

        Assert.Equal("ghe_faketoken123", token);
    }
}
