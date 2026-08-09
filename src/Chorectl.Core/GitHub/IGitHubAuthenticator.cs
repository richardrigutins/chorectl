namespace Chorectl.Core.GitHub;

/// <summary>
/// Obtains a token for authenticating GitHub API requests.
/// </summary>
public interface IGitHubAuthenticator
{
    /// <summary>
    /// Verifies any implementation-specific prerequisites and returns a GitHub API token.
    /// </summary>
    /// <exception cref="GitHubAuthException">The token could not be obtained.</exception>
    string GetToken();
}
