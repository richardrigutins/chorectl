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
/// <param name="mergeMethod">
/// The configured <c>merge_method</c> value ("squash", "merge", or "rebase" - the only values
/// ConfigLoader accepts). Defaults to "squash" to match <see cref="Chorectl.Core.Config.ChorectlConfig.MergeMethod"/>'s
/// own default.
/// </param>
public sealed class OctokitPullRequestMerger(IGitHubClient client, string mergeMethod = "squash") : IPullRequestMerger
{
    public async Task MergeAsync(string owner, DependabotPr pr, CancellationToken cancellationToken = default)
    {
        try
        {
            await client.PullRequest.Merge(owner, pr.Repo, pr.Number, new MergePullRequest { MergeMethod = ParseMergeMethod(mergeMethod) });
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

    /// <summary>
    /// Maps a <c>merge_method</c> config value to Octokit's enum, defaulting to <see cref="PullRequestMergeMethod.Squash"/>
    /// for anything unrecognized (ConfigLoader never actually persists a value other than the three below).
    /// </summary>
    public static PullRequestMergeMethod ParseMergeMethod(string mergeMethod) => mergeMethod switch
    {
        "merge" => PullRequestMergeMethod.Merge,
        "rebase" => PullRequestMergeMethod.Rebase,
        _ => PullRequestMergeMethod.Squash,
    };
}
