using Chorectl.Core.Domain.Dependabot;

namespace Chorectl.Core.GitHub;

/// <summary>
/// Posts a comment on a Dependabot pull request (e.g. <c>@dependabot rebase</c>).
/// </summary>
public interface IPullRequestCommenter
{
    /// <exception cref="GitHubAuthException">Insufficient permission (403) to comment on this PR.</exception>
    /// <exception cref="GitHubRateLimitException">GitHub rate-limited the request.</exception>
    Task CommentAsync(string owner, DependabotPr pr, string body, CancellationToken cancellationToken = default);
}
