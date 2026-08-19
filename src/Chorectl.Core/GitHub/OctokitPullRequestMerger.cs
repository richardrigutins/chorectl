using Chorectl.Core.Domain.Dependabot;
using Octokit;

namespace Chorectl.Core.GitHub;

/// <summary>
/// Merges a Dependabot pull request via the GitHub REST API (Octokit.NET). Translates Octokit's
/// exception types into the domain-specific ones <c>MergeCommand</c> classifies on: a 405 or 409
/// (the head branch changed underneath the request) means "not mergeable yet" and is retryable;
/// a 403 means insufficient permission. Anything else (404, 422, ...) is a likely tool bug and
/// propagates as-is, with its raw message surfaced rather than folded into either of those.
/// </summary>
public sealed class OctokitPullRequestMerger(IGitHubClient client) : IPullRequestMerger
{
    public async Task MergeAsync(string owner, DependabotPr pr, CancellationToken cancellationToken = default)
    {
        try
        {
            await client.PullRequest.Merge(owner, pr.Repo, pr.Number, new MergePullRequest { MergeMethod = PullRequestMergeMethod.Squash });
        }
        catch (Exception ex) when (ex is PullRequestNotMergeableException or PullRequestMismatchException)
        {
            throw new MergeNotReadyException(ex.Message);
        }
        catch (ForbiddenException ex)
        {
            throw new GitHubAuthException(ex.Message);
        }
    }
}
