using Chorectl.Cli.Commands.Dependabot;
using Chorectl.Core.GitHub;
using Spectre.Console.Testing;

namespace Chorectl.Cli.Tests.Commands.Dependabot;

public class RebaseCommandTests
{
    [Fact]
    public async Task RunAsync_WithNoPrsNeedingRebase_PrintsMessageAndRequestsNothing()
    {
        var commenter = new FakePullRequestCommenter();
        var (command, console) = CreateCommand(commenter, SearchResponse());

        var exitCode = await command.RunAsync();

        Assert.Equal(0, exitCode);
        Assert.Contains("No Dependabot PRs need a rebase.", console.Output);
        Assert.Empty(commenter.CommentCalls);
    }

    [Fact]
    public async Task RunAsync_OnlyShowsPrsNeedingRebase()
    {
        var commenter = new FakePullRequestCommenter();
        var (command, console) = CreateCommand(
            commenter,
            SearchResponse(
                Node(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND"),
                Node(2, "Bump right-pad from 1.0.0 to 1.0.1", mergeStateStatus: "CLEAN")));
        console.Input.PushKey(ConsoleKey.Enter);

        await command.RunAsync();

        Assert.Contains("left-pad", console.Output);
        Assert.DoesNotContain("right-pad", console.Output);
    }

    [Fact]
    public async Task RunAsync_IncludesDirtyAndBehindPrs()
    {
        var commenter = new FakePullRequestCommenter();
        var (command, console) = CreateCommand(
            commenter,
            SearchResponse(
                Node(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "DIRTY"),
                Node(2, "Bump right-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND")));
        console.Input.PushKey(ConsoleKey.Enter);

        await command.RunAsync();

        Assert.Equal([1, 2], commenter.CommentCalls.Select(c => c.Pr.Number).Order());
    }

    [Fact]
    public async Task RunAsync_ConfirmingDefaults_RequestsRebaseOnEveryPr_SinceAllArePreselected()
    {
        var commenter = new FakePullRequestCommenter();
        var (command, console) = CreateCommand(
            commenter,
            SearchResponse(
                Node(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND"),
                Node(2, "Bump right-pad from 1.0.0 to 1.0.1", mergeStateStatus: "DIRTY")));
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync();

        Assert.Equal(0, exitCode);
        Assert.Equal([1, 2], commenter.CommentCalls.Select(c => c.Pr.Number).Order());
        Assert.All(commenter.CommentCalls, call => Assert.Equal("@dependabot rebase", call.Body));
        Assert.Contains("requested", console.Output);
        Assert.Contains("Done: 2 requested, 0 failed", console.Output);
    }

    [Fact]
    public async Task RunAsync_LeavesPrsWithTheRebaseBannerUnchecked_ButStillRequestsRebaseForTheRest()
    {
        var commenter = new FakePullRequestCommenter();
        var (command, console) = CreateCommand(
            commenter,
            SearchResponse(
                Node(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND",
                    body: "Dependabot is rebasing this PR due to a merge conflict."),
                Node(2, "Bump right-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND")));
        console.Input.PushKey(ConsoleKey.Enter);

        await command.RunAsync();

        var call = Assert.Single(commenter.CommentCalls);
        Assert.Equal(2, call.Pr.Number);
    }

    [Fact]
    public async Task RunAsync_WithNothingSelected_PrintsMessageAndRequestsNothing()
    {
        var commenter = new FakePullRequestCommenter();
        var (command, console) = CreateCommand(
            commenter,
            SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND")));
        // Cursor starts on the repo group header (pre-checked); space deselects everything under it.
        console.Input.PushKey(ConsoleKey.Spacebar);
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync();

        Assert.Equal(0, exitCode);
        Assert.Contains("No PRs selected. Nothing requested.", console.Output);
        Assert.Empty(commenter.CommentCalls);
    }

    [Fact]
    public async Task RunAsync_DoesNotWaitForCompletion_ItJustReportsRequested()
    {
        var commenter = new FakePullRequestCommenter();
        var (command, console) = CreateCommand(
            commenter,
            SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND")));
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync();

        Assert.Equal(0, exitCode);
        var call = Assert.Single(commenter.CommentCalls);
        Assert.Equal(1, call.Pr.Number);
        Assert.Contains(" ✔ #1", console.Output.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task RunAsync_WhenCommentFailsWithInsufficientPermission_ReportsFailureAndNonZeroExitCode()
    {
        var commenter = new FakePullRequestCommenter { FailWithAuthErrorForPrNumbers = { 1 } };
        var (command, console) = CreateCommand(
            commenter,
            SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND")));
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync();

        Assert.Equal(1, exitCode);
        Assert.Contains("failed — insufficient permission to comment", console.Output);
        Assert.Contains("Done: 0 requested, 1 failed", console.Output);
    }

    [Fact]
    public async Task RunAsync_WhenCommentFailsWithUnrecognizedError_SkipsWithRawErrorSurfaced()
    {
        var commenter = new FakePullRequestCommenter
        {
            FailWithUnexpectedErrorForPrNumbers = { 1 },
            FailureMessage = "422 Validation Failed",
        };
        var (command, console) = CreateCommand(
            commenter,
            SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND")));
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync();

        Assert.Equal(1, exitCode);
        Assert.Contains("failed — unexpected error, likely a tool bug", console.Output);
        Assert.Contains("422 Validation Failed", console.Output);
    }

    [Fact]
    public async Task RunAsync_WhenOnePrFails_ContinuesWithTheRestOfTheBatch()
    {
        var commenter = new FakePullRequestCommenter { FailWithAuthErrorForPrNumbers = { 1 } };
        var (command, console) = CreateCommand(
            commenter,
            SearchResponse(
                Node(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND"),
                Node(2, "Bump right-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND")));
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync();

        Assert.Equal(1, exitCode);
        Assert.Equal([1, 2], commenter.CommentCalls.Select(c => c.Pr.Number).Order());
        Assert.Contains("Done: 1 requested, 1 failed", console.Output);
    }

    [Fact]
    public async Task RunAsync_WithRepo_ScopesDiscoveryAndFetchToThatRepo()
    {
        var commenter = new FakePullRequestCommenter();
        var source = new FakeRepositorySource(
            new RepositoryInfo("octocat", "sample-repo", IsArchived: false, IsFork: false),
            new RepositoryInfo("octocat", "other-repo", IsArchived: false, IsFork: false));
        var restClient = new RestClient(source);
        var handler = new FakeHttpMessageHandler(
            SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND")));
        var graphQlClient = new GraphQlClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") });
        var console = new TestConsole().Interactive();
        console.Input.PushKey(ConsoleKey.Enter);
        var command = new RebaseCommand(restClient, graphQlClient, commenter, console);

        var exitCode = await command.RunAsync("sample-repo");

        Assert.Equal(0, exitCode);
        Assert.False(source.GetOwnedRepositoriesAsyncWasCalled);
        var call = Assert.Single(commenter.CommentCalls);
        Assert.Equal(1, call.Pr.Number);
    }

    [Fact]
    public async Task RunAsync_WithUnknownRepo_ThrowsRepositoryNotFoundException()
    {
        var commenter = new FakePullRequestCommenter();
        var source = new FakeRepositorySource(
            new RepositoryInfo("octocat", "sample-repo", IsArchived: false, IsFork: false));
        var restClient = new RestClient(source);
        var graphQlClient = new GraphQlClient(new HttpClient(new FakeHttpMessageHandler()) { BaseAddress = new Uri("https://api.github.com/") });
        var console = new TestConsole().Interactive();
        var command = new RebaseCommand(restClient, graphQlClient, commenter, console);

        await Assert.ThrowsAsync<RepositoryNotFoundException>(() => command.RunAsync("does-not-exist"));

        Assert.False(source.GetOwnedRepositoriesAsyncWasCalled);
        Assert.Empty(commenter.CommentCalls);
    }

    private static (RebaseCommand Command, TestConsole Console) CreateCommand(
        FakePullRequestCommenter commenter,
        params string[] graphQlResponses)
    {
        var console = new TestConsole().Interactive();
        var restClient = new RestClient(new FakeRepositorySource(
            new RepositoryInfo("octocat", "sample-repo", IsArchived: false, IsFork: false)));
        var handler = new FakeHttpMessageHandler(graphQlResponses);
        var graphQlClient = new GraphQlClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") });
        var command = new RebaseCommand(restClient, graphQlClient, commenter, console);
        return (command, console);
    }

    private static string SearchResponse(params string[] nodes) => $$"""
        {
          "data": {
            "search": {
              "pageInfo": { "hasNextPage": false, "endCursor": null },
              "nodes": [ {{string.Join(",", nodes)}} ]
            }
          }
        }
        """;

    private static string Node(int number, string title, string mergeStateStatus = "CLEAN", string ci = "SUCCESS", string? review = null, string? body = null) => $$"""
        {
          "number": {{number}},
          "title": "{{title}}",
          "url": "https://github.com/octocat/sample-repo/pull/{{number}}",
          "headRefName": "dependabot/some-branch-{{number}}",
          "isDraft": false,
          "updatedAt": "2026-08-01T12:00:00Z",
          "reviewDecision": {{(review is null ? "null" : $"\"{review}\"")}},
          "mergeStateStatus": "{{mergeStateStatus}}",
          "body": {{(body is null ? "null" : $"\"{body}\"")}},
          "repository": { "name": "sample-repo" },
          "labels": { "nodes": [] },
          "commits": { "nodes": [ { "commit": { "statusCheckRollup": { "state": "{{ci}}" } } } ] }
        }
        """;
}
