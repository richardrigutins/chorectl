namespace Chorectl.Core.Update;

/// <summary>
/// A newer chorectl version is available. <see cref="UpdateChecker.CheckAsync"/> returns
/// <see langword="null"/> instead - not this type - whenever nothing should be reported (no newer
/// version, throttled, skipped, or a failed check).
/// </summary>
public sealed record UpdateCheckResult(string LatestVersion);
