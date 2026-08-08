namespace Chorectl.Core.GitHub;

/// <summary>
/// Thrown when the gh CLI prerequisite or authentication check fails.
/// </summary>
public sealed class GitHubAuthException(string message) : Exception(message);
