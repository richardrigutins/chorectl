namespace Chorectl.Core.GitHub;

/// <summary>
/// Looks up the latest chorectl GitHub Release.
/// </summary>
public interface IReleaseSource
{
    Task<ReleaseInfo> GetLatestReleaseAsync();
}
