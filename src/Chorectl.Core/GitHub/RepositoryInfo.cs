namespace Chorectl.Core.GitHub;

/// <summary>
/// A repository owned by the authenticated user, as returned by <see cref="IRepositorySource"/>.
/// </summary>
public sealed record RepositoryInfo(string Owner, string Name, bool IsArchived, bool IsFork);
