using Chorectl.Core.GitHub;
using NSubstitute;
using Octokit;

namespace Chorectl.Core.Tests.GitHub;

public class OctokitReleaseSourceTests
{
    [Fact]
    public async Task GetLatestReleaseAsync_MapsTagNameAndAssets()
    {
        var releases = Substitute.For<IReleasesClient>();
        releases.GetLatest("richardrigutins", "chorectl").Returns(Task.FromResult(FakeRelease(
            "v1.2.3",
            [FakeAsset("chorectl-linux-x64.tar.gz", "https://example.test/chorectl-linux-x64.tar.gz")])));
        var client = FakeClient(releases);

        var result = await new OctokitReleaseSource(client).GetLatestReleaseAsync();

        Assert.Equal("v1.2.3", result.TagName);
        Assert.Equal([new Chorectl.Core.GitHub.ReleaseAsset("chorectl-linux-x64.tar.gz", "https://example.test/chorectl-linux-x64.tar.gz")], result.Assets);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_LooksUpTheChorectlRepo()
    {
        var releases = Substitute.For<IReleasesClient>();
        releases.GetLatest(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.FromResult(FakeRelease("v1.0.0", [])));
        var client = FakeClient(releases);

        await new OctokitReleaseSource(client).GetLatestReleaseAsync();

        await releases.Received(1).GetLatest("richardrigutins", "chorectl");
    }

    private static IGitHubClient FakeClient(IReleasesClient releases)
    {
        var repository = Substitute.For<IRepositoriesClient>();
        repository.Release.Returns(releases);
        var client = Substitute.For<IGitHubClient>();
        client.Repository.Returns(repository);
        return client;
    }

    // Release/ReleaseAsset only expose a full-arity constructor for tests (their properties have
    // private/protected setters, since normal construction goes through Octokit's own JSON
    // deserializer) - values irrelevant to OctokitReleaseSource are filled with placeholders.
    private static Release FakeRelease(string tagName, IReadOnlyList<Octokit.ReleaseAsset> assets) => new(
        url: string.Empty, htmlUrl: string.Empty, assetsUrl: string.Empty, uploadUrl: string.Empty,
        id: 1, nodeId: string.Empty, tagName: tagName, targetCommitish: "main", name: tagName, body: string.Empty,
        draft: false, prerelease: false, createdAt: DateTimeOffset.UtcNow, publishedAt: DateTimeOffset.UtcNow,
        author: null!, tarballUrl: string.Empty, zipballUrl: string.Empty, assets: assets);

    private static Octokit.ReleaseAsset FakeAsset(string name, string browserDownloadUrl) => new(
        url: string.Empty, id: 1, nodeId: string.Empty, name: name, label: string.Empty, state: string.Empty,
        contentType: string.Empty, size: 0, downloadCount: 0, createdAt: DateTimeOffset.UtcNow,
        updatedAt: DateTimeOffset.UtcNow, browserDownloadUrl: browserDownloadUrl, uploader: null!);
}
