namespace Chorectl.Core.GitHub;

/// <summary>
/// Thrown when a repository name filter doesn't match any repo owned by the authenticated user.
/// </summary>
public sealed class RepositoryNotFoundException(string repoName)
    : Exception($"Repo '{repoName}' not found.");
