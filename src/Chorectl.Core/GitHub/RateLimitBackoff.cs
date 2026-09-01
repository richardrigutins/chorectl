namespace Chorectl.Core.GitHub;

/// <summary>
/// Retries an operation that throws <see cref="GitHubRateLimitException"/>, waiting between
/// attempts with exponential backoff (1s, 2s, 4s, ...) capped at a total budget of
/// <c>maxBackoffSeconds</c>. Throws <see cref="RateLimitBackoffExhaustedException"/>, carrying the
/// last rate-limit message, if the budget runs out before an attempt succeeds.
/// </summary>
public static class RateLimitBackoff
{
    private static readonly TimeSpan InitialWait = TimeSpan.FromSeconds(1);

    public static async Task<T> RunAsync<T>(
        Func<Task<T>> action,
        int maxBackoffSeconds,
        Func<TimeSpan, CancellationToken, Task> delay,
        Action<TimeSpan>? onWaiting,
        CancellationToken cancellationToken)
    {
        var budget = TimeSpan.FromSeconds(maxBackoffSeconds);
        var elapsed = TimeSpan.Zero;
        var wait = InitialWait;

        while (true)
        {
            try
            {
                return await action();
            }
            catch (GitHubRateLimitException ex)
            {
                var remaining = budget - elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    throw new RateLimitBackoffExhaustedException(ex.Message);
                }

                var thisWait = wait < remaining ? wait : remaining;
                onWaiting?.Invoke(thisWait);
                await delay(thisWait, cancellationToken);
                elapsed += thisWait;
                wait += wait;
            }
        }
    }

    public static Task RunAsync(
        Func<Task> action,
        int maxBackoffSeconds,
        Func<TimeSpan, CancellationToken, Task> delay,
        Action<TimeSpan>? onWaiting,
        CancellationToken cancellationToken) =>
        RunAsync(async () => { await action(); return true; }, maxBackoffSeconds, delay, onWaiting, cancellationToken);
}
