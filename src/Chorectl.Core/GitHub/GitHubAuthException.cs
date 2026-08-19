namespace Chorectl.Core.GitHub;

/// <summary>
/// Thrown when the gh CLI prerequisite or authentication check fails, or when a specific
/// operation (e.g. a merge) comes back 403 for insufficient permission - the same underlying
/// problem caught at a different time, so both map here.
/// </summary>
public sealed class GitHubAuthException(string message) : Exception(message);
