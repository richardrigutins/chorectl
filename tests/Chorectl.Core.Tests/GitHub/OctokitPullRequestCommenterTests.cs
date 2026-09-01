using System.Net;
using Chorectl.Core.Domain.Dependabot;
using Chorectl.Core.GitHub;
using NSubstitute;
using Octokit;

namespace Chorectl.Core.Tests.GitHub;

public class OctokitPullRequestCommenterTests
{
    [Fact]
    public async Task CommentAsync_OnSuccess_PostsTheCommentBody()
    {
        var comments = Substitute.For<IIssueCommentsClient>();
        var client = FakeClient(comments);

        await new OctokitPullRequestCommenter(client).CommentAsync("octocat", SamplePr(), "@dependabot rebase");

        await comments.Received(1).Create("octocat", "sample-repo", 1, "@dependabot rebase");
    }

    [Fact]
    public async Task CommentAsync_OnRateLimitExceeded_ThrowsGitHubRateLimitException()
    {
        var comments = Substitute.For<IIssueCommentsClient>();
        comments.Create(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<string>())
            .Returns<Task<IssueComment>>(_ => throw new RateLimitExceededException(OctokitTestDoubles.FakeResponse(HttpStatusCode.Forbidden)));
        var client = FakeClient(comments);

        await Assert.ThrowsAsync<GitHubRateLimitException>(() => new OctokitPullRequestCommenter(client).CommentAsync("octocat", SamplePr(), "@dependabot rebase"));
    }

    [Fact]
    public async Task CommentAsync_OnForbidden_ThrowsGitHubAuthException()
    {
        var comments = Substitute.For<IIssueCommentsClient>();
        comments.Create(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<string>())
            .Returns<Task<IssueComment>>(_ => throw new ForbiddenException(OctokitTestDoubles.FakeResponse(HttpStatusCode.Forbidden)));
        var client = FakeClient(comments);

        await Assert.ThrowsAsync<GitHubAuthException>(() => new OctokitPullRequestCommenter(client).CommentAsync("octocat", SamplePr(), "@dependabot rebase"));
    }

    [Fact]
    public async Task CommentAsync_WithAnAlreadyCancelledToken_ThrowsWithoutAttemptingTheRequest()
    {
        var comments = Substitute.For<IIssueCommentsClient>();
        var client = FakeClient(comments);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new OctokitPullRequestCommenter(client).CommentAsync("octocat", SamplePr(), "@dependabot rebase", cts.Token));

        await comments.DidNotReceive().Create(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<string>());
    }

    private static IGitHubClient FakeClient(IIssueCommentsClient comments)
    {
        var issues = Substitute.For<IIssuesClient>();
        issues.Comment.Returns(comments);
        var client = Substitute.For<IGitHubClient>();
        client.Issue.Returns(issues);
        return client;
    }

    private static DependabotPr SamplePr() => new()
    {
        Repo = "sample-repo",
        Number = 1,
        Title = "Bump left-pad from 1.0.0 to 1.0.1",
        Url = "https://github.com/octocat/sample-repo/pull/1",
        HeadRefName = "dependabot/npm_and_yarn/left-pad-1.0.1",
        MergeStateStatus = "BEHIND",
    };
}
