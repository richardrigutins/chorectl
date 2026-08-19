using Chorectl.Core.Domain.Dependabot;

namespace Chorectl.Core.GitHub;

/// <summary>
/// Merges a Dependabot pull request.
/// </summary>
public interface IPullRequestMerger
{
    /// <exception cref="MergeNotReadyException">The PR isn't mergeable yet - a retryable, state-based blocker.</exception>
    /// <exception cref="GitHubAuthException">Insufficient permission (403) to merge this PR.</exception>
    Task MergeAsync(string owner, DependabotPr pr, CancellationToken cancellationToken = default);
}
