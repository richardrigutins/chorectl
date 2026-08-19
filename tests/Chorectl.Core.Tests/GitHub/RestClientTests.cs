using Chorectl.Core.GitHub;

namespace Chorectl.Core.Tests.GitHub;

public class RestClientTests
{
    [Fact]
    public async Task DiscoverReposAsync_ExcludesArchivedRepos()
    {
        var source = new FakeRepositorySource(
            new RepositoryInfo("octocat", "active-repo", IsArchived: false, IsFork: false),
            new RepositoryInfo("octocat", "archived-repo", IsArchived: true, IsFork: false));
        var client = new RestClient(source);

        var repos = await client.DiscoverReposAsync();

        Assert.Equal(["active-repo"], repos.Select(r => r.Name));
    }

    [Fact]
    public async Task DiscoverReposAsync_ExcludesForkedRepos()
    {
        var source = new FakeRepositorySource(
            new RepositoryInfo("octocat", "original-repo", IsArchived: false, IsFork: false),
            new RepositoryInfo("octocat", "forked-repo", IsArchived: false, IsFork: true));
        var client = new RestClient(source);

        var repos = await client.DiscoverReposAsync();

        Assert.Equal(["original-repo"], repos.Select(r => r.Name));
    }

    [Fact]
    public async Task DiscoverReposAsync_WhenNoReposMatch_ReturnsEmpty()
    {
        var source = new FakeRepositorySource(
            new RepositoryInfo("octocat", "archived-repo", IsArchived: true, IsFork: false),
            new RepositoryInfo("octocat", "forked-repo", IsArchived: false, IsFork: true));
        var client = new RestClient(source);

        var repos = await client.DiscoverReposAsync();

        Assert.Empty(repos);
    }

    [Fact]
    public async Task DiscoverReposAsync_ReturnsNonArchivedNonForkRepos()
    {
        var source = new FakeRepositorySource(
            new RepositoryInfo("octocat", "keep-1", IsArchived: false, IsFork: false),
            new RepositoryInfo("octocat", "keep-2", IsArchived: false, IsFork: false),
            new RepositoryInfo("octocat", "drop-archived", IsArchived: true, IsFork: false),
            new RepositoryInfo("octocat", "drop-fork", IsArchived: false, IsFork: true));
        var client = new RestClient(source);

        var repos = await client.DiscoverReposAsync();

        Assert.Equal(["keep-1", "keep-2"], repos.Select(r => r.Name));
    }

    [Fact]
    public async Task DiscoverReposAsync_WithRepoName_ReturnsOnlyThatRepo()
    {
        var source = new FakeRepositorySource(
            new RepositoryInfo("octocat", "repo-a", IsArchived: false, IsFork: false),
            new RepositoryInfo("octocat", "repo-b", IsArchived: false, IsFork: false));
        var client = new RestClient(source);

        var repos = await client.DiscoverReposAsync("repo-b");

        Assert.Equal(["repo-b"], repos.Select(r => r.Name));
    }

    [Fact]
    public async Task DiscoverReposAsync_WithRepoName_DoesNotListEveryRepo()
    {
        var source = new FakeRepositorySource(
            new RepositoryInfo("octocat", "repo-a", IsArchived: false, IsFork: false));
        var client = new RestClient(source);

        await client.DiscoverReposAsync("repo-a");

        Assert.False(source.GetOwnedRepositoriesAsyncWasCalled);
    }

    [Fact]
    public async Task DiscoverReposAsync_WithUnknownRepoName_ThrowsRepositoryNotFoundException()
    {
        var source = new FakeRepositorySource(
            new RepositoryInfo("octocat", "repo-a", IsArchived: false, IsFork: false));
        var client = new RestClient(source);

        await Assert.ThrowsAsync<RepositoryNotFoundException>(() => client.DiscoverReposAsync("does-not-exist"));
    }

    [Fact]
    public async Task DiscoverReposAsync_WithRepoName_WhenRepoIsArchived_ReturnsEmpty()
    {
        var source = new FakeRepositorySource(
            new RepositoryInfo("octocat", "repo-a", IsArchived: true, IsFork: false));
        var client = new RestClient(source);

        var repos = await client.DiscoverReposAsync("repo-a");

        Assert.Empty(repos);
    }

    [Fact]
    public async Task DiscoverReposAsync_WithRepoName_WhenRepoIsFork_ReturnsEmpty()
    {
        var source = new FakeRepositorySource(
            new RepositoryInfo("octocat", "repo-a", IsArchived: false, IsFork: true));
        var client = new RestClient(source);

        var repos = await client.DiscoverReposAsync("repo-a");

        Assert.Empty(repos);
    }
}
