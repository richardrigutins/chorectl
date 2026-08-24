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
        && !IsExcluded(repository.Name);

    // GitHub repo names are case-insensitive (you can't have "MyRepo" and "myrepo" as separate
    // repos under the same owner), but excludeRepos is caller-supplied and might use whatever
    // comparer it was built with - compare explicitly here so a casing mismatch in the config file
    // doesn't silently fail to exclude anything.
    private bool IsExcluded(string repoName) =>
        excludeRepos is not null && excludeRepos.Any(excluded => string.Equals(excluded, repoName, StringComparison.OrdinalIgnoreCase));
}
