namespace Chorectl.Core.GitHub;

/// <summary>
/// One downloadable file attached to a GitHub Release.
/// </summary>
public sealed record ReleaseAsset(string Name, string DownloadUrl);
