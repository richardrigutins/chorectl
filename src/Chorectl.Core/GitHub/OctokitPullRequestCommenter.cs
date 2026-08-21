using Chorectl.Core.Domain.Dependabot;
using Octokit;

namespace Chorectl.Core.GitHub;

/// <summary>
/// Posts a comment on a Dependabot pull request via the GitHub REST API (Octokit.NET). PR
/// comments are issue comments in the GitHub API, so this goes through <c>Issue.Comment</c>.
/// </summary>
public sealed class OctokitPullRequestCommenter(IGitHubClient client) : IPullRequestCommenter
{
    public async Task CommentAsync(string owner, DependabotPr pr, string body, CancellationToken cancellationToken = default)
    {
        try
        {
            await client.Issue.Comment.Create(owner, pr.Repo, pr.Number, body);
        }
        catch (ForbiddenException ex)
        {
            throw new GitHubAuthException(ex.Message);
        }
    }
}
