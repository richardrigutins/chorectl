using System.Net;
using Chorectl.Core.Domain.Dependabot;
using Chorectl.Core.GitHub;
using NSubstitute;
using Octokit;

namespace Chorectl.Core.Tests.GitHub;

public class OctokitPullRequestMergerTests
{
    [Fact]
    public async Task MergeAsync_OnSuccess_MergesUsingTheConfiguredMethod()
    {
        var pullRequests = Substitute.For<IPullRequestsClient>();
        var client = FakeClient(pullRequests);

        await new OctokitPullRequestMerger(client, "rebase").MergeAsync("octocat", SamplePr());

        await pullRequests.Received(1).Merge(
            "octocat",
            "sample-repo",
            1,
            Arg.Is<MergePullRequest>(m => m.MergeMethod == PullRequestMergeMethod.Rebase));
    }

    [Theory]
    [MemberData(nameof(NotMergeableYetExceptions))]
    public async Task MergeAsync_WhenNotMergeableYet_ThrowsMergeNotReadyException(Exception thrown)
    {
        var pullRequests = Substitute.For<IPullRequestsClient>();
        pullRequests.Merge(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<MergePullRequest>())
            .Returns<Task<PullRequestMerge>>(_ => throw thrown);
        var client = FakeClient(pullRequests);

        await Assert.ThrowsAsync<MergeNotReadyException>(() => new OctokitPullRequestMerger(client).MergeAsync("octocat", SamplePr()));
    }

    public static TheoryData<Exception> NotMergeableYetExceptions() => new()
    {
        new PullRequestNotMergeableException(OctokitTestDoubles.FakeResponse(HttpStatusCode.MethodNotAllowed)),
        new PullRequestMismatchException(OctokitTestDoubles.FakeResponse(HttpStatusCode.Conflict)),
    };

    [Fact]
    public async Task MergeAsync_OnRateLimitExceeded_ThrowsGitHubRateLimitException()
    {
        var pullRequests = Substitute.For<IPullRequestsClient>();
        pullRequests.Merge(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<MergePullRequest>())
            .Returns<Task<PullRequestMerge>>(_ => throw new RateLimitExceededException(OctokitTestDoubles.FakeResponse(HttpStatusCode.Forbidden)));
        var client = FakeClient(pullRequests);

        await Assert.ThrowsAsync<GitHubRateLimitException>(() => new OctokitPullRequestMerger(client).MergeAsync("octocat", SamplePr()));
    }

    [Fact]
    public async Task MergeAsync_OnForbidden_ThrowsGitHubAuthException()
    {
        var pullRequests = Substitute.For<IPullRequestsClient>();
        pullRequests.Merge(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<MergePullRequest>())
            .Returns<Task<PullRequestMerge>>(_ => throw new ForbiddenException(OctokitTestDoubles.FakeResponse(HttpStatusCode.Forbidden)));
        var client = FakeClient(pullRequests);

        await Assert.ThrowsAsync<GitHubAuthException>(() => new OctokitPullRequestMerger(client).MergeAsync("octocat", SamplePr()));
    }

    [Fact]
    public async Task MergeAsync_OnAnUnrecognizedFailure_PropagatesTheRawException()
    {
        var pullRequests = Substitute.For<IPullRequestsClient>();
        pullRequests.Merge(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<MergePullRequest>())
            .Returns<Task<PullRequestMerge>>(_ => throw new NotFoundException(OctokitTestDoubles.FakeResponse(HttpStatusCode.NotFound)));
        var client = FakeClient(pullRequests);

        await Assert.ThrowsAsync<NotFoundException>(() => new OctokitPullRequestMerger(client).MergeAsync("octocat", SamplePr()));
    }

    private static IGitHubClient FakeClient(IPullRequestsClient pullRequests)
    {
        var client = Substitute.For<IGitHubClient>();
        client.PullRequest.Returns(pullRequests);
        return client;
    }

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
