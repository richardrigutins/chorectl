namespace Chorectl.Core.GitHub;

/// <summary>
/// Resolves <see cref="Config.ChorectlConfig.GitHubHost"/> into the URLs needed to target either
/// github.com (the default) or a GitHub Enterprise Server instance.
/// </summary>
public static class GitHubHost
{
    public static bool IsDefault(string? host) =>
        string.IsNullOrWhiteSpace(host) || string.Equals(host, "github.com", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// REST API base URI for Octokit, or <see langword="null"/> to use its own github.com default.
    /// </summary>
    public static Uri? RestApiBaseUri(string? host) =>
        IsDefault(host) ? null : new Uri($"https://{host}/api/v3/");

    public static Uri GraphQlBaseUri(string? host) =>
        IsDefault(host) ? new Uri("https://api.github.com/") : new Uri($"https://{host}/api/");
}
