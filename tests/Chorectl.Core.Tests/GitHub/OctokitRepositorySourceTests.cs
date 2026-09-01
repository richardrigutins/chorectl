using Chorectl.Core.GitHub;
using NSubstitute;
using Octokit;

namespace Chorectl.Core.Tests.GitHub;

public class OctokitRepositorySourceTests
{
    [Fact]
    public async Task GetOwnedRepositoriesAsync_MapsOwnerNameArchivedAndFork()
    {
        var repositories = Substitute.For<IRepositoriesClient>();
        repositories.GetAllForCurrent(Arg.Any<RepositoryRequest>())
            .Returns(Task.FromResult<IReadOnlyList<Repository>>([
                FakeRepository("octocat", "active-repo", archived: false, fork: false),
                FakeRepository("octocat", "archived-repo", archived: true, fork: true),
            ]));
        var client = FakeClient(repositories);

        var result = await new OctokitRepositorySource(client).GetOwnedRepositoriesAsync();

        Assert.Equal(
            [new RepositoryInfo("octocat", "active-repo", IsArchived: false, IsFork: false),
             new RepositoryInfo("octocat", "archived-repo", IsArchived: true, IsFork: true)],
            result);
    }

    [Fact]
    public async Task GetOwnedRepositoriesAsync_RequestsOnlyReposOwnedByTheCurrentUser()
    {
        var repositories = Substitute.For<IRepositoriesClient>();
        repositories.GetAllForCurrent(Arg.Any<RepositoryRequest>())
            .Returns(Task.FromResult<IReadOnlyList<Repository>>([]));
        var client = FakeClient(repositories);

        await new OctokitRepositorySource(client).GetOwnedRepositoriesAsync();

        await repositories.Received(1).GetAllForCurrent(Arg.Is<RepositoryRequest>(r => r.Affiliation == RepositoryAffiliation.Owner));
    }

    [Fact]
    public async Task GetOwnedRepositoryAsync_WhenFound_ReturnsIt()
    {
        var repositories = Substitute.For<IRepositoriesClient>();
        repositories.Get("octocat", "sample-repo").Returns(Task.FromResult(FakeRepository("octocat", "sample-repo", archived: false, fork: false)));
        var client = FakeClient(repositories, currentUserLogin: "octocat");

        var result = await new OctokitRepositorySource(client).GetOwnedRepositoryAsync("sample-repo");

        Assert.Equal(new RepositoryInfo("octocat", "sample-repo", IsArchived: false, IsFork: false), result);
    }

    [Fact]
    public async Task GetOwnedRepositoryAsync_LooksUpUnderTheCurrentUsersLogin()
    {
        var repositories = Substitute.For<IRepositoriesClient>();
        repositories.Get("current-user", "sample-repo").Returns(Task.FromResult(FakeRepository("current-user", "sample-repo", archived: false, fork: false)));
        var client = FakeClient(repositories, currentUserLogin: "current-user");

        await new OctokitRepositorySource(client).GetOwnedRepositoryAsync("sample-repo");

        await repositories.Received(1).Get("current-user", "sample-repo");
    }

    [Fact]
    public async Task GetOwnedRepositoryAsync_WhenNotFound_ReturnsNull()
    {
        var repositories = Substitute.For<IRepositoriesClient>();
        repositories.Get(Arg.Any<string>(), Arg.Any<string>())
            .Returns<Task<Repository>>(_ => throw new NotFoundException(OctokitTestDoubles.FakeResponse(System.Net.HttpStatusCode.NotFound)));
        var client = FakeClient(repositories);

        var result = await new OctokitRepositorySource(client).GetOwnedRepositoryAsync("does-not-exist");

        Assert.Null(result);
    }

    private static IGitHubClient FakeClient(IRepositoriesClient repositories, string currentUserLogin = "octocat")
    {
        var users = Substitute.For<IUsersClient>();
        users.Current().Returns(Task.FromResult(FakeUser(currentUserLogin)));
        var client = Substitute.For<IGitHubClient>();
        client.Repository.Returns(repositories);
        client.User.Returns(users);
        return client;
    }

    // Repository/User only expose a full-arity constructor for tests (their properties have
    // private/protected setters, since normal construction goes through Octokit's own
    // JSON deserializer) - values irrelevant to OctokitRepositorySource are filled with
    // placeholders.
    private static Repository FakeRepository(string owner, string name, bool archived, bool fork) => new(
        url: string.Empty, htmlUrl: string.Empty, cloneUrl: string.Empty, gitUrl: string.Empty,
        sshUrl: string.Empty, svnUrl: string.Empty, mirrorUrl: string.Empty, archiveUrl: string.Empty,
        id: 1, nodeId: string.Empty, owner: FakeUser(owner), name: name, fullName: $"{owner}/{name}",
        isTemplate: false, description: string.Empty, homepage: string.Empty, language: string.Empty,
        @private: false, fork: fork, forksCount: 0, stargazersCount: 0, defaultBranch: "main",
        openIssuesCount: 0, pushedAt: null, createdAt: DateTimeOffset.UtcNow, updatedAt: DateTimeOffset.UtcNow,
        permissions: null!, parent: null!, source: null!, license: null!, hasDiscussions: false,
        hasIssues: false, hasWiki: false, hasDownloads: false, hasPages: false, subscribersCount: 0,
        size: 0, allowRebaseMerge: null, allowSquashMerge: null, allowMergeCommit: null, archived: archived,
        watchersCount: 0, deleteBranchOnMerge: null, visibility: RepositoryVisibility.Public, topics: [],
        allowAutoMerge: null, allowUpdateBranch: null, webCommitSignoffRequired: null, securityAndAnalysis: null!);

    private static User FakeUser(string login) => new(
        avatarUrl: string.Empty, bio: string.Empty, blog: string.Empty, collaborators: 0, company: string.Empty,
        createdAt: DateTimeOffset.UtcNow, updatedAt: DateTimeOffset.UtcNow, diskUsage: 0, email: string.Empty,
        followers: 0, following: 0, hireable: null, htmlUrl: string.Empty, totalPrivateRepos: 0, id: 1,
        location: string.Empty, login: login, name: login, nodeId: string.Empty, ownedPrivateRepos: 0,
        plan: null!, privateGists: 0, publicGists: 0, publicRepos: 0, url: string.Empty, permissions: null!,
        siteAdmin: false, ldapDistinguishedName: string.Empty, suspendedAt: null);
}
