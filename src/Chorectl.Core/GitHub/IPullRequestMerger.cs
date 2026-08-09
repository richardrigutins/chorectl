using Chorectl.Core.Domain.Dependabot;

namespace Chorectl.Core.GitHub;

/// <summary>
/// Merges a Dependabot pull request.
/// </summary>
public interface IPullRequestMerger
{
    Task MergeAsync(string owner, DependabotPr pr, CancellationToken cancellationToken = default);
}
