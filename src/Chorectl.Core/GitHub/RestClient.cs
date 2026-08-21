namespace Chorectl.Core.GitHub;

/// <summary>
/// Wraps GitHub REST API calls needed for repo discovery and Dependabot PR actions.
/// </summary>
public sealed class RestClient(IRepositorySource repositorySource, IReadOnlySet<string>? excludeRepos = null, bool includeForks = false)
{
    /// <summary>
    /// Lists the authenticated user's non-archived repos (personal account only), minus anything
    /// in <paramref name="excludeRepos"/> and, unless <paramref name="includeForks"/> is set,
    /// minus forks. When <paramref name="repoName"/> is given, looks up just that repo instead of
    /// listing every repo and filtering afterwards.
    /// </summary>
    public async Task<IReadOnlyList<RepositoryInfo>> DiscoverReposAsync(string? repoName = null)
    {
        if (repoName is not null)
        {
            var repository = await repositorySource.GetOwnedRepositoryAsync(repoName)
                ?? throw new RepositoryNotFoundException(repoName);
            return IsEligible(repository) ? [repository] : [];
        }

        var repositories = await repositorySource.GetOwnedRepositoriesAsync();

        return repositories
            .Where(IsEligible)
            .ToList();
    }

    private bool IsEligible(RepositoryInfo repository) =>
        !repository.IsArchived
        && (includeForks || !repository.IsFork)
        && (excludeRepos is null || !excludeRepos.Contains(repository.Name));
}
