using Chorectl.Core.Domain.Dependabot;
using Octokit;

namespace Chorectl.Core.GitHub;

/// <summary>
/// Submits an approving review on a Dependabot pull request via the GitHub REST API (Octokit.NET).
/// A rate-limit response is a 403 in Octokit's own hierarchy (<c>RateLimitExceededException</c>
/// subclasses <c>ForbiddenException</c>), so it's caught first and kept distinct from a genuine
/// permission problem.
/// </summary>
public sealed class OctokitPullRequestApprover(IGitHubClient client) : IPullRequestApprover
{
    public async Task ApproveAsync(string owner, DependabotPr pr, CancellationToken cancellationToken = default)
    {
        try
        {
            await client.PullRequest.Review.Create(owner, pr.Repo, pr.Number, new PullRequestReviewCreate { Event = PullRequestReviewEvent.Approve });
        }
        catch (RateLimitExceededException ex)
        {
            throw new GitHubRateLimitException(ex.Message);
        }
        catch (ForbiddenException ex)
        {
            throw new GitHubAuthException(ex.Message);
        }
    }
}
