namespace Chorectl.Core.GitHub;

/// <summary>
/// Thrown by <see cref="RateLimitBackoff"/> when GitHub keeps rate-limiting a request past
/// <c>max_backoff_seconds</c> of exponential backoff. Distinct from <see cref="GitHubRateLimitException"/>
/// (a single rate-limited response, which <see cref="RateLimitBackoff"/> retries transparently) so
/// callers can tell "still retrying" apart from "gave up" - a rate limit is account-wide, so giving
/// up here means the whole batch should stop rather than move on to the next PR.
/// </summary>
public sealed class RateLimitBackoffExhaustedException(string message) : Exception(message);
