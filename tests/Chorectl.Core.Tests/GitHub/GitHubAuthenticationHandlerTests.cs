using System.Net;
using Chorectl.Core.GitHub;

namespace Chorectl.Core.Tests.GitHub;

public class GitHubAuthenticationHandlerTests
{
    [Fact]
    public async Task SendAsync_AttachesTheAuthenticatorsTokenAsABearerHeader()
    {
        var authenticator = new FakeGitHubAuthenticator(() => "gho_token");
        var recordingHandler = new RecordingInnerHandler();
        using var httpClient = new HttpClient(new GitHubAuthenticationHandler(authenticator) { InnerHandler = recordingHandler });

        await httpClient.GetAsync("https://api.github.com/");

        Assert.Equal("Bearer", recordingHandler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("gho_token", recordingHandler.LastRequest?.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task SendAsync_FetchesTheTokenOnlyOncePerRequest_NotAtConstructionTime()
    {
        var authenticator = new FakeGitHubAuthenticator(() => "gho_token");
        using var httpClient = new HttpClient(new GitHubAuthenticationHandler(authenticator) { InnerHandler = new RecordingInnerHandler() });

        Assert.Equal(0, authenticator.CallCount);

        await httpClient.GetAsync("https://api.github.com/");

        Assert.Equal(1, authenticator.CallCount);
    }

    private sealed class RecordingInnerHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
