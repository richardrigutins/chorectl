namespace Chorectl.Core.GitHub;

/// <summary>
/// Thrown when GitHub reports a pull request isn't mergeable yet - branch protection catching up,
/// a mid-flight rebase, or the head branch changing underneath the request. A retryable,
/// state-based blocker; the only failure type <c>MergeCommand</c> polls and retries on.
/// </summary>
public sealed class MergeNotReadyException(string message) : Exception(message);
