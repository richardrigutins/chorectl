using Chorectl.Cli.Commands.Dependabot;
using Chorectl.Core.GitHub;
using Spectre.Console.Testing;

namespace Chorectl.Cli.Tests.Commands.Dependabot;

public class MergeCommandTests
{
    [Fact]
    public async Task RunAsync_WithNoReadyPrs_PrintsMessageAndMergesNothing()
    {
        var merger = new FakePullRequestMerger();
        var (command, console) = CreateCommand(merger, SearchResponse());

        var exitCode = await command.RunAsync();

        Assert.Equal(0, exitCode);
        Assert.Contains("No Dependabot PRs are ready to merge.", console.Output);
        Assert.Empty(merger.MergeCalls);
    }

    [Fact]
    public async Task RunAsync_OnlyShowsReadyPrs_AndConfirmingDefaultsMergesJustThePreselectedOne()
    {
        var merger = new FakePullRequestMerger();
        var (command, console) = CreateCommand(
            merger,
            SearchResponse(
                Node(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "CLEAN", ci: "SUCCESS"),
                Node(2, "Bump right-pad from 1.0.0 to 1.0.1", mergeStateStatus: "CLEAN", ci: "FAILURE")),
            ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1"));
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync();

        Assert.Equal(0, exitCode);
        Assert.Contains("left-pad", console.Output);
        Assert.DoesNotContain("right-pad", console.Output);
        var call = Assert.Single(merger.MergeCalls);
        Assert.Equal(1, call.Pr.Number);
    }

    [Fact]
    public async Task RunAsync_SelectionScreen_ShowsTheVersionsBeingUpgradedTo()
    {
        var merger = new FakePullRequestMerger();
        var (command, console) = CreateCommand(
            merger,
            SearchResponse(Node(1, "Bump firebase-tools from 11.2.0 to 11.3.1")));
        console.Input.PushKey(ConsoleKey.Enter);

        await command.RunAsync();

        Assert.Contains("11.2.0 -> 11.3.1", console.Output);
    }

    [Fact]
    public async Task RunAsync_SelectionScreen_FallsBackToTheRawTitle_WhenItDoesntMatchDependabotsFormat()
    {
        var merger = new FakePullRequestMerger();
        var (command, console) = CreateCommand(
            merger,
            SearchResponse(Node(1, "Bump the aws-sdk group with 3 updates")));
        console.Input.PushKey(ConsoleKey.Enter);

        await command.RunAsync();

        Assert.Contains("Bump the aws-sdk group with 3 updates", console.Output);
    }

    [Fact]
    public async Task RunAsync_SelectionScreen_ChecksRepoGroupHeader_WhenAllPrsInThatRepoAreDefaultSelected()
    {
        var merger = new FakePullRequestMerger();
        var (command, console) = CreateCommand(
            merger,
            [
                SearchResponse(
                    Node(1, "Bump patch-dep from 1.0.0 to 1.0.1"),
                    Node(2, "Bump minor-dep from 1.0.0 to 1.1.0")),
                ByNumberResponse(1, "Bump patch-dep from 1.0.0 to 1.0.1"),
                ByNumberResponse(2, "Bump minor-dep from 1.0.0 to 1.1.0"),
            ],
            delay: NoOpDelay);
        console.Input.PushKey(ConsoleKey.Enter);

        await command.RunAsync();

        Assert.Contains("[X] sample-repo", console.Output);
    }

    [Fact]
    public async Task RunAsync_SelectionScreen_LeavesRepoGroupHeaderUnchecked_WhenNotAllPrsAreDefaultSelected()
    {
        var merger = new FakePullRequestMerger();
        var (command, console) = CreateCommand(
            merger,
            SearchResponse(
                Node(1, "Bump patch-dep from 1.0.0 to 1.0.1"),
                Node(2, "Bump major-dep from 1.0.0 to 2.0.0")),
            ByNumberResponse(1, "Bump patch-dep from 1.0.0 to 1.0.1"));
        console.Input.PushKey(ConsoleKey.Enter);

        await command.RunAsync();

        Assert.Contains("[ ] sample-repo", console.Output);
        Assert.DoesNotContain("[X] sample-repo", console.Output);
    }

    [Fact]
    public async Task RunAsync_DefaultSelection_ExcludesMajorBumps()
    {
        var merger = new FakePullRequestMerger();
        var (command, console) = CreateCommand(
            merger,
            SearchResponse(
                Node(1, "Bump patch-dep from 1.0.0 to 1.0.1"),
                Node(2, "Bump major-dep from 1.0.0 to 2.0.0")),
            ByNumberResponse(1, "Bump patch-dep from 1.0.0 to 1.0.1"));
        console.Input.PushKey(ConsoleKey.Enter);

        await command.RunAsync();

        var call = Assert.Single(merger.MergeCalls);
        Assert.Equal(1, call.Pr.Number);
    }

    [Fact]
    public async Task RunAsync_TogglingTheRepoGroupHeader_SelectsEveryPrInThatRepo()
    {
        var merger = new FakePullRequestMerger();
        var (command, console) = CreateCommand(
            merger,
            [
                SearchResponse(
                    Node(1, "Bump patch-dep from 1.0.0 to 1.0.1"),
                    Node(2, "Bump major-dep from 1.0.0 to 2.0.0")),
                ByNumberResponse(1, "Bump patch-dep from 1.0.0 to 1.0.1"),
                ByNumberResponse(2, "Bump major-dep from 1.0.0 to 2.0.0"),
            ],
            delay: NoOpDelay);
        // Cursor starts on the repo group header; space there toggles every PR underneath it.
        console.Input.PushKey(ConsoleKey.Spacebar);
        console.Input.PushKey(ConsoleKey.Enter);

        await command.RunAsync();

        Assert.Equal([1, 2], merger.MergeCalls.Select(c => c.Pr.Number).Order());
    }

    [Fact]
    public async Task RunAsync_ArrowDownThenSpace_TogglesTheNavigatedToPr()
    {
        var merger = new FakePullRequestMerger();
        var (command, console) = CreateCommand(
            merger,
            [
                SearchResponse(
                    Node(1, "Bump patch-dep from 1.0.0 to 1.0.1"),
                    Node(2, "Bump major-dep from 1.0.0 to 2.0.0")),
                ByNumberResponse(1, "Bump patch-dep from 1.0.0 to 1.0.1"),
                ByNumberResponse(2, "Bump major-dep from 1.0.0 to 2.0.0"),
            ],
            delay: NoOpDelay);
        // Cursor: group header -> down to patch-dep (already selected) -> down to major-dep -> toggle it on.
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Spacebar);
        console.Input.PushKey(ConsoleKey.Enter);

        await command.RunAsync();

        Assert.Equal([1, 2], merger.MergeCalls.Select(c => c.Pr.Number).Order());
    }

    [Fact]
    public async Task RunAsync_WithNothingSelected_PrintsMessageAndMergesNothing()
    {
        var merger = new FakePullRequestMerger();
        var (command, console) = CreateCommand(
            merger,
            SearchResponse(Node(1, "Bump major-dep from 1.0.0 to 2.0.0")));
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync();

        Assert.Equal(0, exitCode);
        Assert.Contains("No PRs selected. Nothing merged.", console.Output);
        Assert.Empty(merger.MergeCalls);
    }

    [Fact]
    public async Task RunAsync_WhenRefetchShowsStateChanged_SkipsWithoutMerging()
    {
        var merger = new FakePullRequestMerger();
        var (command, console) = CreateCommand(
            merger,
            SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1")),
            ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "DIRTY"));
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync();

        Assert.Equal(0, exitCode);
        Assert.Empty(merger.MergeCalls);
        Assert.Contains("skipped", console.Output);
    }

    [Fact]
    public async Task RunAsync_WhenRefetchShowsPrClosed_SkipsWithoutMerging()
    {
        var merger = new FakePullRequestMerger();
        var (command, console) = CreateCommand(
            merger,
            SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1")),
            """{ "data": { "repository": { "pullRequest": null } } }""");
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync();

        Assert.Equal(0, exitCode);
        Assert.Empty(merger.MergeCalls);
        Assert.Contains("no longer open", console.Output);
    }

    [Fact]
    public async Task RunAsync_WaitsBetweenMergesOnTheSameRepoButNotAfterTheLastOne()
    {
        var merger = new FakePullRequestMerger();
        var waits = new List<TimeSpan>();
        var (command, console) = CreateCommand(
            merger,
            [
                SearchResponse(
                    Node(1, "Bump patch-dep-1 from 1.0.0 to 1.0.1"),
                    Node(2, "Bump patch-dep-2 from 1.0.0 to 1.0.1")),
                ByNumberResponse(1, "Bump patch-dep-1 from 1.0.0 to 1.0.1"),
                ByNumberResponse(2, "Bump patch-dep-2 from 1.0.0 to 1.0.1"),
            ],
            delay: (wait, _) =>
            {
                waits.Add(wait);
                return Task.CompletedTask;
            });
        console.Input.PushKey(ConsoleKey.Enter);

        await command.RunAsync();

        Assert.Equal(2, merger.MergeCalls.Count);
        var wait = Assert.Single(waits);
        Assert.Equal(TimeSpan.FromSeconds(30), wait);
    }

    [Fact]
    public async Task RunAsync_SelectionScreen_DoesNotThrow_WhenTitleContainsMarkupCharacters()
    {
        var merger = new FakePullRequestMerger();
        var (command, console) = CreateCommand(
            merger,
            SearchResponse(Node(1, "Bump [special]/pkg from 1.0.0 to 1.0.1")));
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync();

        Assert.Equal(0, exitCode);
        Assert.Contains("[special]/pkg", console.Output);
    }

    [Fact]
    public async Task RunAsync_WhenMergeFailsWithBracketsInTheMessage_RendersTheReasonWithoutThrowing()
    {
        var merger = new FakePullRequestMerger { FailForPrNumbers = { 1 }, FailureMessage = "422 [validation_failed]: merge blocked" };
        var (command, console) = CreateCommand(
            merger,
            SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1")),
            ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1"));
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync();

        Assert.Equal(1, exitCode);
        Assert.Contains("[validation_failed]", console.Output);
    }

    [Fact]
    public async Task RunAsync_WhenOnePrFailsToMerge_ContinuesWithTheRestOfTheBatch()
    {
        var merger = new FakePullRequestMerger { FailForPrNumbers = { 1 } };
        var (command, console) = CreateCommand(
            merger,
            [
                SearchResponse(
                    Node(1, "Bump patch-dep-1 from 1.0.0 to 1.0.1"),
                    Node(2, "Bump patch-dep-2 from 1.0.0 to 1.0.1")),
                ByNumberResponse(1, "Bump patch-dep-1 from 1.0.0 to 1.0.1"),
                ByNumberResponse(2, "Bump patch-dep-2 from 1.0.0 to 1.0.1"),
            ]);
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync();

        Assert.Equal(1, exitCode);
        Assert.Equal([1, 2], merger.MergeCalls.Select(c => c.Pr.Number));
        Assert.Contains("failed", console.Output);
        Assert.Contains("Done: 1 merged, 0 skipped, 1 failed", console.Output);
    }

    private static Task NoOpDelay(TimeSpan wait, CancellationToken cancellationToken) => Task.CompletedTask;

    private static (MergeCommand Command, TestConsole Console) CreateCommand(
        FakePullRequestMerger merger,
        params string[] graphQlResponses) =>
        CreateCommand(merger, graphQlResponses, delay: null);

    private static (MergeCommand Command, TestConsole Console) CreateCommand(
        FakePullRequestMerger merger,
        string[] graphQlResponses,
        Func<TimeSpan, CancellationToken, Task>? delay)
    {
        var console = new TestConsole().Interactive();
        var restClient = new RestClient(new FakeRepositorySource(
            new RepositoryInfo("octocat", "sample-repo", IsArchived: false, IsFork: false)));
        var handler = new FakeHttpMessageHandler(graphQlResponses);
        var graphQlClient = new GraphQlClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") });
        var command = new MergeCommand(restClient, graphQlClient, merger, console, delay);
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

    private static string Node(int number, string title, string mergeStateStatus = "CLEAN", string ci = "SUCCESS", string? review = null) => $$"""
        {
          "number": {{number}},
          "title": "{{title}}",
          "url": "https://github.com/octocat/sample-repo/pull/{{number}}",
          "headRefName": "dependabot/some-branch-{{number}}",
          "isDraft": false,
          "updatedAt": "2026-08-01T12:00:00Z",
          "reviewDecision": {{(review is null ? "null" : $"\"{review}\"")}},
          "mergeStateStatus": "{{mergeStateStatus}}",
          "repository": { "name": "sample-repo" },
          "labels": { "nodes": [] },
          "commits": { "nodes": [ { "commit": { "statusCheckRollup": { "state": "{{ci}}" } } } ] }
        }
        """;

    private static string ByNumberResponse(int number, string title, string mergeStateStatus = "CLEAN", string ci = "SUCCESS", string? review = null, string state = "OPEN") => $$"""
        {
          "data": {
            "repository": {
              "pullRequest": {
                "number": {{number}},
                "title": "{{title}}",
                "url": "https://github.com/octocat/sample-repo/pull/{{number}}",
                "headRefName": "dependabot/some-branch-{{number}}",
                "isDraft": false,
                "updatedAt": "2026-08-01T12:00:00Z",
                "reviewDecision": {{(review is null ? "null" : $"\"{review}\"")}},
                "mergeStateStatus": "{{mergeStateStatus}}",
                "state": "{{state}}",
                "repository": { "name": "sample-repo" },
                "labels": { "nodes": [] },
                "commits": { "nodes": [ { "commit": { "statusCheckRollup": { "state": "{{ci}}" } } } ] }
              }
            }
          }
        }
        """;
}
