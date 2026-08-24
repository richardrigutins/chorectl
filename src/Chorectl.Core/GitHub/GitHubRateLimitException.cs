namespace Chorectl.Core.GitHub;

/// <summary>
/// Thrown when GitHub responds with a rate-limit error. Octokit.NET's own
/// <c>RateLimitExceededException</c> subclasses <c>ForbiddenException</c>, so it must be caught
/// ahead of <see cref="GitHubAuthException"/> at each call site - otherwise a rate limit gets
/// misreported as "insufficient permission", which is both wrong and not actionable the same way.
/// Full backoff-and-retry handling is Phase 3 (US-11); for now this lets a rate-limited action be
/// skipped and reported with an accurate reason instead.
/// </summary>
public sealed class GitHubRateLimitException(string message) : Exception(message);
