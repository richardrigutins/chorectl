namespace Chorectl.Core.GitHub;

/// <summary>
/// Wraps another <see cref="IGitHubAuthenticator"/> and caches its token in memory after the
/// first successful call, so the multiple GitHub clients that each need a token only trigger
/// one underlying authentication per process.
/// </summary>
public sealed class CachingGitHubAuthenticator(IGitHubAuthenticator inner) : IGitHubAuthenticator
{
    private readonly Lazy<string> token = new(inner.GetToken);

    public string GetToken() => token.Value;
}
