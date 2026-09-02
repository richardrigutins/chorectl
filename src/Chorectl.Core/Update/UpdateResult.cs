namespace Chorectl.Core.Update;

/// <summary>Outcome of <see cref="Updater.UpdateAsync"/>.</summary>
public sealed record UpdateResult(bool Updated, string CurrentVersion, string LatestVersion);
