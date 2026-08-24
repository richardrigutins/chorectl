using Chorectl.Core.Domain.Dependabot;
using Octokit;

namespace Chorectl.Core.GitHub;

/// <summary>
/// Posts a comment on a Dependabot pull request via the GitHub REST API (Octokit.NET). PR
/// comments are issue comments in the GitHub API, so this goes through <c>Issue.Comment</c>. A
/// rate-limit response is a 403 in Octokit's own hierarchy (<c>RateLimitExceededException</c>
/// subclasses <c>ForbiddenException</c>), so it's caught first and kept distinct from a genuine
/// permission problem.
/// </summary>
public sealed class OctokitPullRequestCommenter(IGitHubClient client) : IPullRequestCommenter
{
    public async Task CommentAsync(string owner, DependabotPr pr, string body, CancellationToken cancellationToken = default)
    {
        // Octokit.NET's IssueComments.Create has no CancellationToken overload, so a cancellation
        // requested while this call is already in flight can't stop it - this only catches the
        // case where the token was already cancelled before this method could start.
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await client.Issue.Comment.Create(owner, pr.Repo, pr.Number, body);
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
