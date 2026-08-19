namespace Chorectl.Core.GitHub;

/// <summary>
/// Wraps GitHub REST API calls needed for repo discovery and Dependabot PR actions.
/// </summary>
public sealed class RestClient(IRepositorySource repositorySource)
{
    /// <summary>
    /// Lists the authenticated user's non-archived, non-fork repos (personal account only).
    /// When <paramref name="repoName"/> is given, looks up just that repo instead of listing
    /// every repo and filtering afterwards.
    /// </summary>
    public async Task<IReadOnlyList<RepositoryInfo>> DiscoverReposAsync(string? repoName = null)
    {
        if (repoName is not null)
        {
            var repository = await repositorySource.GetOwnedRepositoryAsync(repoName)
                ?? throw new RepositoryNotFoundException(repoName);
            return repository is { IsArchived: false, IsFork: false } ? [repository] : [];
        }

        var repositories = await repositorySource.GetOwnedRepositoriesAsync();

        return repositories
            .Where(r => !r.IsArchived && !r.IsFork)
            .ToList();
    }
}
