using Chorectl.Core.Domain.Dependabot;
using Chorectl.Core.GitHub;
using Octokit;

namespace Chorectl.Core.Tests.GitHub;

public class OctokitPullRequestMergerTests
{
    [Theory]
    [InlineData("squash", PullRequestMergeMethod.Squash)]
    [InlineData("merge", PullRequestMergeMethod.Merge)]
    [InlineData("rebase", PullRequestMergeMethod.Rebase)]
    public void ParseMergeMethod_MapsTheConfiguredValueToTheMatchingOctokitEnum(string mergeMethod, PullRequestMergeMethod expected)
    {
        Assert.Equal(expected, OctokitPullRequestMerger.ParseMergeMethod(mergeMethod));
    }

    [Fact]
    public void ParseMergeMethod_WithAnUnrecognizedValue_FallsBackToSquash()
    {
        Assert.Equal(PullRequestMergeMethod.Squash, OctokitPullRequestMerger.ParseMergeMethod("not-a-real-method"));
    }

    [Fact]
    public async Task MergeAsync_WithAnAlreadyCancelledToken_ThrowsWithoutAttemptingTheRequest()
    {
        // Octokit.NET's PullRequest.Merge has no CancellationToken overload of its own, so this is
        // the only cancellation chorectl can honor here: reject before the request is even sent,
        // rather than silently ignoring the token. A real (but never-actually-called) GitHubClient
        // is enough to prove that, since the guard must fire before any HTTP call is attempted.
        var client = new GitHubClient(new ProductHeaderValue("chorectl-tests"));
        var merger = new OctokitPullRequestMerger(client);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => merger.MergeAsync("octocat", SamplePr(), cts.Token));
    }

    private static DependabotPr SamplePr() => new()
    {
        Repo = "sample-repo",
        Number = 1,
        Title = "Bump left-pad from 1.0.0 to 1.0.1",
        Url = "https://github.com/octocat/sample-repo/pull/1",
        HeadRefName = "dependabot/npm_and_yarn/left-pad-1.0.1",
        MergeStateStatus = "CLEAN",
    };
}
