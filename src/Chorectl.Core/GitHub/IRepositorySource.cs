namespace Chorectl.Core.GitHub;

/// <summary>
/// Lists repositories owned by the authenticated user.
/// </summary>
public interface IRepositorySource
{
    Task<IReadOnlyList<RepositoryInfo>> GetOwnedRepositoriesAsync();
}
