using System.Net;
using Chorectl.Core.Domain.Dependabot;
using Chorectl.Core.GitHub;
using NSubstitute;
using Octokit;

namespace Chorectl.Core.Tests.GitHub;

public class OctokitPullRequestApproverTests
{
    [Fact]
    public async Task ApproveAsync_OnSuccess_SubmitsAnApprovingReview()
    {
        var reviews = Substitute.For<IPullRequestReviewsClient>();
        var client = FakeClient(reviews);

        await new OctokitPullRequestApprover(client).ApproveAsync("octocat", SamplePr());

        await reviews.Received(1).Create(
            "octocat",
            "sample-repo",
            1,
            Arg.Is<PullRequestReviewCreate>(r => r.Event == PullRequestReviewEvent.Approve));
    }

    [Fact]
    public async Task ApproveAsync_OnRateLimitExceeded_ThrowsGitHubRateLimitException()
    {
        var reviews = Substitute.For<IPullRequestReviewsClient>();
        reviews.Create(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<PullRequestReviewCreate>())
            .Returns<Task<PullRequestReview>>(_ => throw new RateLimitExceededException(OctokitTestDoubles.FakeResponse(HttpStatusCode.Forbidden)));
        var client = FakeClient(reviews);

        await Assert.ThrowsAsync<GitHubRateLimitException>(() => new OctokitPullRequestApprover(client).ApproveAsync("octocat", SamplePr()));
    }

    [Fact]
    public async Task ApproveAsync_OnForbidden_ThrowsGitHubAuthException()
    {
        var reviews = Substitute.For<IPullRequestReviewsClient>();
        reviews.Create(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<PullRequestReviewCreate>())
            .Returns<Task<PullRequestReview>>(_ => throw new ForbiddenException(OctokitTestDoubles.FakeResponse(HttpStatusCode.Forbidden)));
        var client = FakeClient(reviews);

        await Assert.ThrowsAsync<GitHubAuthException>(() => new OctokitPullRequestApprover(client).ApproveAsync("octocat", SamplePr()));
    }

    [Fact]
    public async Task ApproveAsync_WithAnAlreadyCancelledToken_ThrowsWithoutAttemptingTheRequest()
    {
        var reviews = Substitute.For<IPullRequestReviewsClient>();
        var client = FakeClient(reviews);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new OctokitPullRequestApprover(client).ApproveAsync("octocat", SamplePr(), cts.Token));

        await reviews.DidNotReceive().Create(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<PullRequestReviewCreate>());
    }

    private static IGitHubClient FakeClient(IPullRequestReviewsClient reviews)
    {
        var pullRequests = Substitute.For<IPullRequestsClient>();
        pullRequests.Review.Returns(reviews);
        var client = Substitute.For<IGitHubClient>();
        client.PullRequest.Returns(pullRequests);
        return client;
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
