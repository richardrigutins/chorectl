using Chorectl.Core.Domain.Dependabot;
using Octokit;

namespace Chorectl.Core.GitHub;

/// <summary>
/// Merges a Dependabot pull request via the GitHub REST API (Octokit.NET).
/// </summary>
public sealed class OctokitPullRequestMerger(IGitHubClient client) : IPullRequestMerger
{
    public Task MergeAsync(string owner, DependabotPr pr, CancellationToken cancellationToken = default) =>
        client.PullRequest.Merge(owner, pr.Repo, pr.Number, new MergePullRequest { MergeMethod = PullRequestMergeMethod.Squash });
}
