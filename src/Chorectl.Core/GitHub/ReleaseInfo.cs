namespace Chorectl.Core.GitHub;

/// <summary>
/// A GitHub Release: its tag (e.g. <c>v1.2.3</c>) and downloadable assets.
/// </summary>
public sealed record ReleaseInfo(string TagName, IReadOnlyList<ReleaseAsset> Assets);
