using Chorectl.Core.Domain.Dependabot;

namespace Chorectl.Core.GitHub;

/// <summary>
/// Submits an approving review on a Dependabot pull request.
/// </summary>
public interface IPullRequestApprover
{
    /// <exception cref="GitHubAuthException">Insufficient permission (403) to review this PR.</exception>
    Task ApproveAsync(string owner, DependabotPr pr, CancellationToken cancellationToken = default);
}
