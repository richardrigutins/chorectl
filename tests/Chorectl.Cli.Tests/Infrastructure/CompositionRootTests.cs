using System.Net;
using Chorectl.Cli.Infrastructure;
using Chorectl.Core.Config;
using Chorectl.Core.Domain.Dependabot;
using Chorectl.Core.GitHub;
using Chorectl.Core.Update;
using Microsoft.Extensions.DependencyInjection;
using Octokit;
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
    public async Task Run_ThroughThePublicEntryPoint_WithHelpFlag_UsesTheRealGhCliAuthenticatorWithoutCrashing()
    {
        // The public overload is what Program.cs calls: it builds a real GhCliAuthenticator
        // internally - --help must still never touch it, or config, even though gh itself may not
        // be installed/authenticated in the test environment.
        var console = new TestConsole();

        var exitCode = await CompositionRoot.RunAsync(["--help"], console);

        Assert.Equal(0, exitCode);
        Assert.Contains("USAGE", console.Output);
    }

    [Fact]
    public async Task Run_WithVersionFlag_PrintsTheVersionAndExits_WithoutCallingTheAuthenticator()
    {
        var authenticator = new FakeGitHubAuthenticator(new GitHubAuthException("gh is not authenticated"));
        var console = new TestConsole();

        var exitCode = await CompositionRoot.RunAsync(authenticator, ["--version"], console, UpdateCheckCachePath);

        Assert.False(authenticator.WasCalled);
        Assert.Equal(0, exitCode);
        Assert.Equal(CurrentVersion.Value, console.Output.Trim());
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

        // A real command also triggers the startup update check, which authenticates separately -
        // without a fake here it would shell out to the real gh (and GitHub) from a unit test.
        var releaseAuthenticator = new FakeGitHubAuthenticator(new GitHubAuthException("release check not under test"));

        var exitCode = await CompositionRoot.RunAsync(authenticator, ["dependabot", "list"], console, UpdateCheckCachePath, releaseAuthenticator);

        Assert.True(authenticator.WasCalled);
        Assert.Equal(1, exitCode);
        Assert.Contains("gh is not authenticated", console.Output);
    }

    // `config get` never talks to GitHub on its own, so it's the cleanest seam for proving the
    // update check itself runs (or doesn't) independently of the real command's own GitHub calls.

    [Fact]
    public async Task Run_ConfigGet_TriggersTheUpdateCheck_WhichFailsSilentlyAndNeverBreaksTheRealCommand()
    {
        // The release check authenticates separately from the target host (see CompositionRoot's
        // releaseAuthenticator doc comment), so it's passed the same fake here to observe it too.
        var authenticator = new FakeGitHubAuthenticator(new GitHubAuthException("gh is not authenticated"));
        var console = new TestConsole();

        var exitCode = await CompositionRoot.RunAsync(authenticator, ["config", "get"], console, UpdateCheckCachePath, releaseAuthenticator: authenticator);

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

    [Fact]
    public void BuildServices_WithGitHubHostConfigured_TargetsThatHostsRestApi()
    {
        using var provider = BuildProvider("github.mycompany.com");

        var client = provider.GetRequiredService<IGitHubClient>();

        Assert.Equal(new Uri("https://github.mycompany.com/api/v3/"), client.Connection.BaseAddress);
    }

    [Fact]
    public void BuildServices_WithNoGitHubHost_TargetsGithubComsRestApi()
    {
        using var provider = BuildProvider(host: "");

        var client = provider.GetRequiredService<IGitHubClient>();

        Assert.Equal(new Uri("https://api.github.com/"), client.Connection.BaseAddress);
    }

    [Theory]
    [InlineData("github.mycompany.com", "https://github.mycompany.com/api/graphql")]
    [InlineData("", "https://api.github.com/graphql")]
    public async Task BuildServices_SendsGraphQlRequestsToTheConfiguredHost(string host, string expectedUri)
    {
        var handler = new CapturingHandler();
        using var provider = BuildProvider(host, handler);
        var graphQl = provider.GetRequiredService<GraphQlClient>();
        var pr = new DependabotPr { Repo = "repo", Number = 1, Title = "t", Url = "u", HeadRefName = "h", MergeStateStatus = "CLEAN" };

        await graphQl.RefetchAsync("octocat", pr);

        Assert.Equal(new Uri(expectedUri), handler.RequestUri);
    }

    [Fact]
    public void BuildReleaseClient_AlwaysTargetsGithubCom()
    {
        var client = CompositionRoot.BuildReleaseClient(new FakeGitHubAuthenticator());

        Assert.Equal(new Uri("https://api.github.com/"), client.Connection.BaseAddress);
    }

    private ServiceProvider BuildProvider(string host, HttpMessageHandler? graphQlInnerHandler = null)
    {
        var configLoader = new ConfigLoader(Path.Combine(_tempDir, "config.yml"));
        configLoader.SetValue("github_host", host);

        return CompositionRoot.BuildServices(
                new FakeGitHubAuthenticator(), new TestConsole(), configLoader, new Lazy<ChorectlConfig>(configLoader.Load), graphQlInnerHandler)
            .BuildServiceProvider();
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":{"repository":null}}""", System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    [Theory]
    [InlineData(new object[] { new string[] { } })]
    [InlineData(new object[] { new[] { "--help" } })]
    [InlineData(new object[] { new[] { "-h" } })]
    [InlineData(new object[] { new[] { "update" } })]
    [InlineData(new object[] { new[] { "UPDATE" } })]
    [InlineData(new object[] { new[] { "-v" } })]
    [InlineData(new object[] { new[] { "--version" } })]
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
