namespace Chorectl.Core.GitHub;

/// <summary>
/// Lists repositories owned by the authenticated user.
/// </summary>
public interface IRepositorySource
{
    Task<IReadOnlyList<RepositoryInfo>> GetOwnedRepositoriesAsync();

    /// <summary>
    /// Looks up a single repo owned by the authenticated user by name, without listing every repo.
    /// Returns <see langword="null"/> if no such repo exists.
    /// </summary>
    Task<RepositoryInfo?> GetOwnedRepositoryAsync(string name);
}
