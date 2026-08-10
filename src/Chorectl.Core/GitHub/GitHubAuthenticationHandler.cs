using System.Net.Http.Headers;

namespace Chorectl.Core.GitHub;

/// <summary>
/// Attaches the GitHub API bearer token to outgoing requests via <see cref="IGitHubAuthenticator"/>,
/// so the token is fetched only when the first request is actually sent, not when the
/// <see cref="HttpClient"/> is constructed.
/// </summary>
public sealed class GitHubAuthenticationHandler(IGitHubAuthenticator authenticator) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authenticator.GetToken());
        return base.SendAsync(request, cancellationToken);
    }
}
