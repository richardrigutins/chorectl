namespace Chorectl.Core.GitHub;

/// <summary>
/// Thrown when GitHub responds with a rate-limit error. Octokit.NET's own
/// <c>RateLimitExceededException</c> subclasses <c>ForbiddenException</c>, so it must be caught
/// ahead of <see cref="GitHubAuthException"/> at each call site - otherwise a rate limit gets
/// misreported as "insufficient permission", which is both wrong and not actionable the same way.
/// <see cref="RateLimitBackoff"/> retries an operation that throws this transparently, with
/// exponential backoff up to <c>max_backoff_seconds</c> (US-11).
/// </summary>
public sealed class GitHubRateLimitException(string message) : Exception(message);
