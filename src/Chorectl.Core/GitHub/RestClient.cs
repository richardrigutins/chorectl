namespace Chorectl.Core.GitHub;

/// <summary>
/// Wraps GitHub REST API calls needed for repo discovery and Dependabot PR actions.
/// </summary>
public sealed class RestClient(IRepositorySource repositorySource)
{
    /// <summary>
    /// Lists the authenticated user's non-archived, non-fork repos (personal account only).
    /// </summary>
    public async Task<IReadOnlyList<RepositoryInfo>> DiscoverReposAsync()
    {
        var repositories = await repositorySource.GetOwnedRepositoriesAsync();

        return repositories
            .Where(r => !r.IsArchived && !r.IsFork)
            .ToList();
    }
}
