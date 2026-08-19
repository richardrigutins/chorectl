using Octokit;

namespace Chorectl.Core.GitHub;

/// <summary>
/// Lists repositories owned by the authenticated user via the GitHub REST API (Octokit.NET).
/// </summary>
public sealed class OctokitRepositorySource(IGitHubClient client) : IRepositorySource
{
    public async Task<IReadOnlyList<RepositoryInfo>> GetOwnedRepositoriesAsync()
    {
        var request = new RepositoryRequest { Affiliation = RepositoryAffiliation.Owner };
        var repositories = await client.Repository.GetAllForCurrent(request);

        return repositories
            .Select(r => new RepositoryInfo(r.Owner.Login, r.Name, r.Archived, r.Fork))
            .ToList();
    }

    public async Task<RepositoryInfo?> GetOwnedRepositoryAsync(string name)
    {
        var currentUser = await client.User.Current();

        try
        {
            var repository = await client.Repository.Get(currentUser.Login, name);
            return new RepositoryInfo(repository.Owner.Login, repository.Name, repository.Archived, repository.Fork);
        }
        catch (NotFoundException)
        {
            return null;
        }
    }
}
