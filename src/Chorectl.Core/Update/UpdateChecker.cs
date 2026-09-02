using System.Text.Json;
using Chorectl.Core.Config;
using Chorectl.Core.GitHub;

namespace Chorectl.Core.Update;

/// <summary>
/// Checks for a newer chorectl release, throttled to once per 24h via a cache file at
/// <paramref name="cachePath"/> (normally <see cref="DefaultCachePath"/>, alongside the config
/// file). Fails silently - any exception, including a cancellation from a caller-imposed timeout,
/// results in <see langword="null"/>, never a thrown error, so a slow or unreachable GitHub never
/// blocks or errors the actual command.
/// </summary>
/// <param name="now">Overridable for tests; defaults to <see cref="DateTimeOffset.UtcNow"/>.</param>
public sealed class UpdateChecker(IReleaseSource releaseSource, string cachePath, Func<DateTimeOffset>? now = null)
{
    private static readonly TimeSpan ThrottleInterval = TimeSpan.FromHours(24);

    private readonly Func<DateTimeOffset> now = now ?? (() => DateTimeOffset.UtcNow);

    /// <summary>
    /// The XDG-style default cache path, alongside <see cref="ConfigLoader.DefaultPath"/>.
    /// </summary>
    public static string DefaultCachePath { get; } =
        Path.Combine(Path.GetDirectoryName(ConfigLoader.DefaultPath)!, "update-check.json");

    /// <summary>
    /// Returns the latest version if it's newer than <paramref name="currentVersion"/> and a check
    /// is actually due - <see langword="null"/> for "skip" (<paramref name="skipUpdateCheck"/>),
    /// "throttled" (checked within the last 24h), "up to date", or "check failed", all of which
    /// look the same to the caller: nothing to report.
    /// </summary>
    public async Task<UpdateCheckResult?> CheckAsync(string currentVersion, bool skipUpdateCheck, CancellationToken cancellationToken = default)
    {
        if (skipUpdateCheck)
        {
            return null;
        }

        try
        {
            var checkedAt = now();
            if (ReadLastCheckedAt() is { } lastChecked && checkedAt - lastChecked < ThrottleInterval)
            {
                return null;
            }

            // Written before the network call, so a check that's due but fails (GitHub down,
            // timeout) still starts a fresh throttle window instead of being retried on every
            // single invocation until GitHub recovers.
            WriteLastCheckedAt(checkedAt);

            var release = await releaseSource.GetLatestReleaseAsync().WaitAsync(cancellationToken);

            return VersionComparer.IsNewer(release.TagName, currentVersion)
                ? new UpdateCheckResult(release.TagName)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private DateTimeOffset? ReadLastCheckedAt()
    {
        if (!File.Exists(cachePath))
        {
            return null;
        }

        var cache = JsonSerializer.Deserialize<UpdateCheckCache>(File.ReadAllText(cachePath));
        return cache?.LastCheckedAt;
    }

    private void WriteLastCheckedAt(DateTimeOffset value)
    {
        var directory = Path.GetDirectoryName(cachePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(cachePath, JsonSerializer.Serialize(new UpdateCheckCache(value)));
    }

    private sealed record UpdateCheckCache(DateTimeOffset LastCheckedAt);
}
