using Octokit;

namespace Chorectl.Core.GitHub;

/// <summary>
/// Supplies GitHub API credentials to Octokit.NET via <see cref="IGitHubAuthenticator"/>, so the
/// token is fetched only when Octokit actually needs to send a request, not when the client is
/// constructed.
/// </summary>
public sealed class GitHubCredentialStore(IGitHubAuthenticator authenticator) : ICredentialStore
{
    public Task<Credentials> GetCredentials() => Task.FromResult(new Credentials(authenticator.GetToken()));
}
