using Octokit;

namespace Chorectl.Core.GitHub;

/// <summary>
/// Looks up the latest chorectl GitHub Release via the REST API (Octokit.NET).
/// </summary>
public sealed class OctokitReleaseSource(IGitHubClient client) : IReleaseSource
{
    private const string Owner = "richardrigutins";
    private const string Repo = "chorectl";

    public async Task<ReleaseInfo> GetLatestReleaseAsync()
    {
        var release = await client.Repository.Release.GetLatest(Owner, Repo);

        return new ReleaseInfo(
            release.TagName,
            release.Assets.Select(a => new ReleaseAsset(a.Name, a.BrowserDownloadUrl)).ToList());
    }
}
