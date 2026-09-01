using Chorectl.Core.GitHub;

namespace Chorectl.Core.Tests.GitHub;

public class RateLimitBackoffTests
{
    [Fact]
    public async Task RunAsync_WhenActionSucceedsFirstTry_ReturnsResultWithoutWaiting()
    {
        var waits = new List<TimeSpan>();

        var result = await RateLimitBackoff.RunAsync(
            () => Task.FromResult(42),
            maxBackoffSeconds: 10,
            delay: NoOpDelay,
            onWaiting: waits.Add,
            CancellationToken.None);

        Assert.Equal(42, result);
        Assert.Empty(waits);
    }

    [Fact]
    public async Task RunAsync_WhenRateLimitedOnce_WaitsThenRetriesAndReturnsResult()
    {
        var attempts = 0;
        var waits = new List<TimeSpan>();

        var result = await RateLimitBackoff.RunAsync(
            () =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw new GitHubRateLimitException("rate limited");
                }

                return Task.FromResult("done");
            },
            maxBackoffSeconds: 10,
            delay: NoOpDelay,
            onWaiting: waits.Add,
            CancellationToken.None);

        Assert.Equal("done", result);
        Assert.Equal(2, attempts);
        Assert.Equal([TimeSpan.FromSeconds(1)], waits);
    }

    [Fact]
    public async Task RunAsync_DoublesTheWaitOnEachSuccessiveRateLimit()
    {
        var waits = new List<TimeSpan>();
        var attempts = 0;

        await RateLimitBackoff.RunAsync(
            () =>
            {
                attempts++;
                if (attempts <= 3)
                {
                    throw new GitHubRateLimitException("rate limited");
                }

                return Task.FromResult(true);
            },
            maxBackoffSeconds: 60,
            delay: NoOpDelay,
            onWaiting: waits.Add,
            CancellationToken.None);

        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)], waits);
    }

    [Fact]
    public async Task RunAsync_ClampsTheFinalWaitToTheRemainingBudgetInsteadOfOvershooting()
    {
        var waits = new List<TimeSpan>();
        var attempts = 0;

        await RateLimitBackoff.RunAsync(
            () =>
            {
                attempts++;
                if (attempts <= 2)
                {
                    throw new GitHubRateLimitException("rate limited");
                }

                return Task.FromResult(true);
            },
            maxBackoffSeconds: 2,
            delay: NoOpDelay,
            onWaiting: waits.Add,
            CancellationToken.None);

        // 1s then 2s would total 3s, over the 2s budget - the second wait clamps to the 1s left.
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)], waits);
    }

    [Fact]
    public async Task RunAsync_WhenBudgetIsExhausted_ThrowsRateLimitBackoffExhaustedExceptionWithTheLastMessage()
    {
        var exception = await Assert.ThrowsAsync<RateLimitBackoffExhaustedException>(() =>
            RateLimitBackoff.RunAsync(
                () => throw new GitHubRateLimitException("still rate limited"),
                maxBackoffSeconds: 1,
                delay: NoOpDelay,
                onWaiting: null,
                CancellationToken.None));

        Assert.Equal("still rate limited", exception.Message);
    }

    [Fact]
    public async Task RunAsync_WithZeroMaxBackoffSeconds_GivesUpOnTheFirstRateLimitWithoutWaiting()
    {
        var waits = new List<TimeSpan>();

        await Assert.ThrowsAsync<RateLimitBackoffExhaustedException>(() =>
            RateLimitBackoff.RunAsync(
                () => throw new GitHubRateLimitException("rate limited"),
                maxBackoffSeconds: 0,
                delay: NoOpDelay,
                onWaiting: waits.Add,
                CancellationToken.None));

        Assert.Empty(waits);
    }

    [Fact]
    public async Task RunAsync_NonGenericOverload_RunsTheActionAndPropagatesFailuresTheSameWay()
    {
        var attempts = 0;

        await RateLimitBackoff.RunAsync(
            () =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw new GitHubRateLimitException("rate limited");
                }

                return Task.CompletedTask;
            },
            maxBackoffSeconds: 10,
            delay: NoOpDelay,
            onWaiting: null,
            CancellationToken.None);

        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task RunAsync_WhenActionThrowsSomethingElse_PropagatesWithoutRetrying()
    {
        var attempts = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RateLimitBackoff.RunAsync<object>(
                () =>
                {
                    attempts++;
                    throw new InvalidOperationException("not a rate limit");
                },
                maxBackoffSeconds: 10,
                delay: NoOpDelay,
                onWaiting: null,
                CancellationToken.None));

        Assert.Equal(1, attempts);
    }

    private static Task NoOpDelay(TimeSpan wait, CancellationToken cancellationToken) => Task.CompletedTask;
}
