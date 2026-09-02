using Chorectl.Core.GitHub;
using Chorectl.Core.Update;

namespace Chorectl.Core.Tests.Update;

public class UpdateCheckerTests : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory("chorectl-tests-").FullName;

    private string CachePath => Path.Combine(_tempDir, "update-check.json");

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public async Task CheckAsync_WhenSkipUpdateCheckIsTrue_ReturnsNullWithoutCallingReleaseSource()
    {
        var releaseSource = new FakeReleaseSource(new ReleaseInfo("v9.9.9", []));
        var checker = new UpdateChecker(releaseSource, CachePath);

        var result = await checker.CheckAsync(currentVersion: "1.0.0", skipUpdateCheck: true);

        Assert.Null(result);
        Assert.Equal(0, releaseSource.CallCount);
    }

    [Fact]
    public async Task CheckAsync_WhenNoNewerVersionExists_ReturnsNull()
    {
        var releaseSource = new FakeReleaseSource(new ReleaseInfo("v1.0.0", []));
        var checker = new UpdateChecker(releaseSource, CachePath);

        var result = await checker.CheckAsync(currentVersion: "1.0.0", skipUpdateCheck: false);

        Assert.Null(result);
    }

    [Fact]
    public async Task CheckAsync_WhenANewerVersionExists_ReturnsIt()
    {
        var releaseSource = new FakeReleaseSource(new ReleaseInfo("v1.1.0", []));
        var checker = new UpdateChecker(releaseSource, CachePath);

        var result = await checker.CheckAsync(currentVersion: "1.0.0", skipUpdateCheck: false);

        Assert.NotNull(result);
        Assert.Equal("v1.1.0", result.LatestVersion);
    }

    [Fact]
    public async Task CheckAsync_WhenReleaseSourceThrows_ReturnsNullSilently()
    {
        var releaseSource = new FakeReleaseSource(failWith: new HttpRequestException("boom"));
        var checker = new UpdateChecker(releaseSource, CachePath);

        var result = await checker.CheckAsync(currentVersion: "1.0.0", skipUpdateCheck: false);

        Assert.Null(result);
    }

    [Fact]
    public async Task CheckAsync_WhenTheReleaseSourceHangs_ReturnsNullOnceCancelled()
    {
        var releaseSource = new FakeReleaseSource(neverCompletes: true);
        var checker = new UpdateChecker(releaseSource, CachePath);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var result = await checker.CheckAsync(currentVersion: "1.0.0", skipUpdateCheck: false, cts.Token);

        Assert.Null(result);
    }

    [Fact]
    public async Task CheckAsync_CalledTwiceWithinTheThrottleWindow_OnlyCallsTheReleaseSourceOnce()
    {
        var releaseSource = new FakeReleaseSource(new ReleaseInfo("v1.1.0", []));
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var checker = new UpdateChecker(releaseSource, CachePath, now: () => now);

        await checker.CheckAsync(currentVersion: "1.0.0", skipUpdateCheck: false);
        var second = await checker.CheckAsync(currentVersion: "1.0.0", skipUpdateCheck: false);

        Assert.Null(second);
        Assert.Equal(1, releaseSource.CallCount);
    }

    [Fact]
    public async Task CheckAsync_CalledAfterTheThrottleWindowElapses_CallsTheReleaseSourceAgain()
    {
        var releaseSource = new FakeReleaseSource(new ReleaseInfo("v1.1.0", []));
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var checker = new UpdateChecker(releaseSource, CachePath, now: () => now);
        await checker.CheckAsync(currentVersion: "1.0.0", skipUpdateCheck: false);

        now = now.AddHours(24).AddSeconds(1);
        await checker.CheckAsync(currentVersion: "1.0.0", skipUpdateCheck: false);

        Assert.Equal(2, releaseSource.CallCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTheReleaseSourceFails_StillThrottlesTheNextCall()
    {
        // A failed check (network down, GitHub unreachable) still marks the throttle window, so a
        // downed API doesn't get hit on every single invocation for the next 24h.
        var releaseSource = new FakeReleaseSource(failWith: new HttpRequestException("boom"));
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var checker = new UpdateChecker(releaseSource, CachePath, now: () => now);

        await checker.CheckAsync(currentVersion: "1.0.0", skipUpdateCheck: false);
        await checker.CheckAsync(currentVersion: "1.0.0", skipUpdateCheck: false);

        Assert.Equal(1, releaseSource.CallCount);
    }
}
