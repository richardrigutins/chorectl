using Chorectl.Cli.Commands.Dependabot;
using Chorectl.Core.Config;
using Chorectl.Core.GitHub;
using Spectre.Console.Testing;

namespace Chorectl.Cli.Tests.Commands.Dependabot;

public class MergeCommandTests
{
    [Fact]
    public async Task RunAsync_WhenPrIsMerged_RecordsAMergedAuditEntry()
    {
        var merger = new FakePullRequestMerger();
        var auditLog = new FakeAuditLog();
        var (command, console) = CreateCommand(
            merger,
            [
                SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1")),
                ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1"),
            ],
            delay: NoOpDelay,
            auditLog: auditLog);
        console.Input.PushKey(ConsoleKey.Enter);

        await command.RunAsync(Settings());

        var entry = Assert.Single(auditLog.Entries);
        Assert.Equal("sample-repo", entry.Repo);
        Assert.Equal(1, entry.PrNumber);
        Assert.Equal("merged", entry.Action);
        Assert.Null(entry.Reason);
    }

    [Fact]
    public async Task RunAsync_WhenPrIsSkipped_RecordsASkippedAuditEntryWithReason()
    {
        var merger = new FakePullRequestMerger();
        var auditLog = new FakeAuditLog();
        var (command, console) = CreateCommand(
            merger,
            [
                SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1")),
                ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "DIRTY"),
            ],
            delay: NoOpDelay,
            auditLog: auditLog);
        console.Input.PushKey(ConsoleKey.Enter);

        await command.RunAsync(Settings());

        var entry = Assert.Single(auditLog.Entries);
        Assert.Equal("skipped", entry.Action);
        Assert.Equal("conflicting", entry.Reason);
    }

    [Fact]
    public async Task RunAsync_WhenAuditLogWriteFails_StillProcessesTheRestOfTheBatchAndWarns()
    {
        var merger = new FakePullRequestMerger();
        var auditLog = new FakeAuditLog { FailWith = new IOException("disk full") };
        var (command, console) = CreateCommand(
            merger,
            [
                SearchResponse(
                    Node(1, "Bump patch-dep from 1.0.0 to 1.0.1"),
                    Node(2, "Bump minor-dep from 1.0.0 to 1.1.0")),
                ByNumberResponse(1, "Bump patch-dep from 1.0.0 to 1.0.1"),
                ByNumberResponse(2, "Bump minor-dep from 1.0.0 to 1.1.0"),
            ],
            delay: NoOpDelay,
            auditLog: auditLog);
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync(Settings());

        Assert.Equal(0, exitCode);
        Assert.Equal([1, 2], merger.MergeCalls.Select(c => c.Pr.Number).OrderBy(n => n));
        Assert.Empty(auditLog.Entries);
        Assert.Contains("Warning:", console.Output);
        Assert.Contains("disk full", console.Output);
        Assert.Contains("Done: 2 merged, 0 skipped, 0 failed", console.Output);
    }

    [Fact]
    public async Task RunAsync_WithDryRun_RecordsNoAuditEntries()
    {
        var merger = new FakePullRequestMerger();
        var auditLog = new FakeAuditLog();
        var (command, console) = CreateCommand(
            merger,
            [
                SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1")),
                ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1"),
            ],
            delay: NoOpDelay,
            auditLog: auditLog);
        console.Input.PushKey(ConsoleKey.Enter);

        await command.RunAsync(Settings(dryRun: true));

        Assert.Empty(auditLog.Entries);
    }

    [Fact]
    public async Task RunAsync_WithNoReadyPrs_PrintsMessageAndMergesNothing()
    {
        var merger = new FakePullRequestMerger();
        var (command, console) = CreateCommand(merger, SearchResponse());

        var exitCode = await command.RunAsync(Settings());

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

        var exitCode = await command.RunAsync(Settings());

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

        await command.RunAsync(Settings());

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

        await command.RunAsync(Settings());

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

        await command.RunAsync(Settings());

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

        await command.RunAsync(Settings());

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

        await command.RunAsync(Settings());

        var call = Assert.Single(merger.MergeCalls);
        Assert.Equal(1, call.Pr.Number);
    }

    [Fact]
    public async Task RunAsync_WithMajorEnabledInConfig_YesMergesMajorBumpsToo()
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
            delay: NoOpDelay,
            defaultSelect: new DefaultSelectConfig { Patch = true, Minor = true, Major = true });

        await command.RunAsync(Settings(yes: true));

        Assert.Equal([1, 2], merger.MergeCalls.Select(c => c.Pr.Number).OrderBy(n => n));
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

        await command.RunAsync(Settings());

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

        await command.RunAsync(Settings());

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

        var exitCode = await command.RunAsync(Settings());

        Assert.Equal(0, exitCode);
        Assert.Contains("No PRs selected. Nothing merged.", console.Output);
        Assert.Empty(merger.MergeCalls);
    }

    [Fact]
    public async Task RunAsync_WhenRefetchShowsDirty_SkipsImmediatelyWithNoMergeAttemptAndNoPoll()
    {
        var merger = new FakePullRequestMerger();
        var waits = new List<TimeSpan>();
        var (command, console) = CreateCommand(
            merger,
            [
                SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1")),
                ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "DIRTY"),
            ],
            delay: (wait, _) =>
            {
                waits.Add(wait);
                return Task.CompletedTask;
            });
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync(Settings());

        Assert.Equal(0, exitCode);
        Assert.Empty(merger.MergeCalls);
        Assert.Empty(waits);
        Assert.Contains("skipped", console.Output);
    }

    [Fact]
    public async Task RunAsync_WhenRefetchShowsBehindButNotDirty_MergesDirectlyWithNoWait()
    {
        var merger = new FakePullRequestMerger();
        var waits = new List<TimeSpan>();
        var (command, console) = CreateCommand(
            merger,
            [
                SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND")),
                ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND"),
            ],
            delay: (wait, _) =>
            {
                waits.Add(wait);
                return Task.CompletedTask;
            });
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync(Settings());

        Assert.Equal(0, exitCode);
        var call = Assert.Single(merger.MergeCalls);
        Assert.Equal(1, call.Pr.Number);
        Assert.Empty(waits);
        Assert.Contains("merged", console.Output);
    }

    [Fact]
    public async Task RunAsync_WhenMergeFailsThenPrIsNoLongerBehindAndCiIsPassing_PollsAndRetriesUntilItMerges()
    {
        var merger = new FakePullRequestMerger { FailOnceForPrNumbers = { 1 } };
        var (command, console) = CreateCommand(
            merger,
            [
                SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND")),
                ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND"),
                ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "CLEAN"),
            ],
            delay: NoOpDelay,
            pollInterval: TimeSpan.FromSeconds(1),
            pollTimeout: TimeSpan.FromSeconds(5));
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync(Settings());

        Assert.Equal(0, exitCode);
        Assert.Equal([1, 1], merger.MergeCalls.Select(c => c.Pr.Number));
        Assert.Contains("polling", console.Output);
        Assert.Contains("4s left", console.Output);
        Assert.Contains("merged", console.Output);
    }

    [Fact]
    public async Task RunAsync_WhenBodyHasDependabotsRebaseBanner_ShowsRebaseInProgressWhilePolling()
    {
        var merger = new FakePullRequestMerger { FailForPrNumbers = { 1 } };
        var (command, console) = CreateCommand(
            merger,
            [
                SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND")),
                ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND",
                    body: "Dependabot is rebasing this PR due to a merge conflict."),
                ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND"),
            ],
            delay: NoOpDelay,
            pollInterval: TimeSpan.FromSeconds(1),
            pollTimeout: TimeSpan.FromSeconds(1));
        console.Input.PushKey(ConsoleKey.Enter);

        await command.RunAsync(Settings());

        Assert.Contains("rebase in progress, polling", console.Output);
    }

    [Fact]
    public async Task RunAsync_WhenBodyHasNoRebaseBanner_ShowsGenericPollingMessage()
    {
        var merger = new FakePullRequestMerger { FailForPrNumbers = { 1 } };
        var (command, console) = CreateCommand(
            merger,
            [
                SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND")),
                ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND"),
                ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND"),
            ],
            delay: NoOpDelay,
            pollInterval: TimeSpan.FromSeconds(1),
            pollTimeout: TimeSpan.FromSeconds(1));
        console.Input.PushKey(ConsoleKey.Enter);

        await command.RunAsync(Settings());

        Assert.Contains("polling", console.Output);
        Assert.DoesNotContain("rebase in progress", console.Output);
    }

    [Fact]
    public async Task RunAsync_WhenPrBecomesDirtyMidPoll_StopsPollingImmediatelyInsteadOfRunningOutTheTimeout()
    {
        var merger = new FakePullRequestMerger { FailForPrNumbers = { 1 } };
        var waits = new List<TimeSpan>();
        var (command, console) = CreateCommand(
            merger,
            [
                SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND")),
                ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND"),
                ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "DIRTY"),
            ],
            delay: (wait, _) =>
            {
                waits.Add(wait);
                return Task.CompletedTask;
            },
            pollInterval: TimeSpan.FromSeconds(1),
            pollTimeout: TimeSpan.FromSeconds(10));
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync(Settings());

        Assert.Equal(0, exitCode);
        var wait = Assert.Single(waits);
        Assert.Equal(TimeSpan.FromSeconds(1), wait);
        Assert.Contains("became conflicting while waiting", console.Output);
    }

    [Fact]
    public async Task RunAsync_WhenPollTimeoutElapsesStillBlocked_SkipsWithReasonInsteadOfForceMerging()
    {
        var merger = new FakePullRequestMerger { FailForPrNumbers = { 1 } };
        var (command, console) = CreateCommand(
            merger,
            [
                SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND")),
                ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND"),
                ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND"),
                ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND"),
            ],
            delay: NoOpDelay,
            pollInterval: TimeSpan.FromSeconds(1),
            pollTimeout: TimeSpan.FromSeconds(2));
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync(Settings());

        Assert.Equal(0, exitCode);
        var call = Assert.Single(merger.MergeCalls);
        Assert.Equal(1, call.Pr.Number);
        Assert.Contains("still not mergeable after 2s", console.Output);
    }

    [Fact]
    public async Task RunAsync_WhenPollIntervalExceedsTimeout_ClampsTheWaitToTheRemainingBudgetInsteadOfOvershooting()
    {
        var merger = new FakePullRequestMerger { FailForPrNumbers = { 1 } };
        var waits = new List<TimeSpan>();
        var (command, console) = CreateCommand(
            merger,
            [
                SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND")),
                ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND"),
                ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND"),
            ],
            delay: (wait, _) =>
            {
                waits.Add(wait);
                return Task.CompletedTask;
            },
            pollInterval: TimeSpan.FromSeconds(10),
            pollTimeout: TimeSpan.FromSeconds(2));
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync(Settings());

        Assert.Equal(0, exitCode);
        var wait = Assert.Single(waits);
        Assert.Equal(TimeSpan.FromSeconds(2), wait);
        Assert.Contains("still not mergeable after 2s", console.Output);
    }

    [Fact]
    public async Task RunAsync_WhenMergeFailsWithInsufficientPermission_SkipsImmediatelyWithoutPolling()
    {
        var merger = new FakePullRequestMerger { FailWithAuthErrorForPrNumbers = { 1 } };
        var waits = new List<TimeSpan>();
        var (command, console) = CreateCommand(
            merger,
            [
                SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1")),
                ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1"),
            ],
            delay: (wait, _) =>
            {
                waits.Add(wait);
                return Task.CompletedTask;
            });
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync(Settings());

        Assert.Equal(1, exitCode);
        var call = Assert.Single(merger.MergeCalls);
        Assert.Equal(1, call.Pr.Number);
        Assert.Empty(waits);
        Assert.Contains("failed — insufficient permission to merge", console.Output);
    }

    [Fact]
    public async Task RunAsync_WhenMergeFailsWithRateLimit_SkipsImmediatelyWithoutPollingAndDoesNotReportInsufficientPermission()
    {
        var merger = new FakePullRequestMerger { FailWithRateLimitErrorForPrNumbers = { 1 } };
        var waits = new List<TimeSpan>();
        var (command, console) = CreateCommand(
            merger,
            [
                SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1")),
                ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1"),
            ],
            delay: (wait, _) =>
            {
                waits.Add(wait);
                return Task.CompletedTask;
            });
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync(Settings());

        Assert.Equal(1, exitCode);
        var call = Assert.Single(merger.MergeCalls);
        Assert.Equal(1, call.Pr.Number);
        Assert.Empty(waits);
        Assert.Contains("failed — rate limited by GitHub", console.Output);
        Assert.DoesNotContain("insufficient permission", console.Output);
    }

    [Fact]
    public async Task RunAsync_WhenMergeFailsWithUnrecognizedError_SkipsImmediatelyWithRawErrorSurfaced()
    {
        var merger = new FakePullRequestMerger
        {
            FailWithUnexpectedErrorForPrNumbers = { 1 },
            FailureMessage = "422 Validation Failed",
        };
        var waits = new List<TimeSpan>();
        var (command, console) = CreateCommand(
            merger,
            [
                SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1")),
                ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1"),
            ],
            delay: (wait, _) =>
            {
                waits.Add(wait);
                return Task.CompletedTask;
            });
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync(Settings());

        Assert.Equal(1, exitCode);
        Assert.Empty(waits);
        Assert.Contains("failed — unexpected error, likely a tool bug", console.Output);
        Assert.Contains("422 Validation Failed", console.Output);
    }

    [Fact]
    public async Task RunAsync_WhenPollRetryFailsWithInsufficientPermission_StopsPollingImmediatelyInsteadOfRunningOutTheTimeout()
    {
        var merger = new FakePullRequestMerger { FailPollRetryWithAuthErrorForPrNumbers = { 1 } };
        var (command, console) = CreateCommand(
            merger,
            [
                SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND")),
                ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND"),
                ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "CLEAN"),
            ],
            delay: NoOpDelay,
            pollInterval: TimeSpan.FromSeconds(1),
            pollTimeout: TimeSpan.FromSeconds(10));
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync(Settings());

        Assert.Equal(1, exitCode);
        Assert.Equal([1, 1], merger.MergeCalls.Select(c => c.Pr.Number));
        Assert.Contains("failed — insufficient permission to merge", console.Output);
    }

    [Fact]
    public async Task RunAsync_WhenPollRetryFailsWithRateLimit_StopsPollingImmediatelyWithoutReportingInsufficientPermission()
    {
        var merger = new FakePullRequestMerger { FailPollRetryWithRateLimitErrorForPrNumbers = { 1 } };
        var (command, console) = CreateCommand(
            merger,
            [
                SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND")),
                ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND"),
                ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "CLEAN"),
            ],
            delay: NoOpDelay,
            pollInterval: TimeSpan.FromSeconds(1),
            pollTimeout: TimeSpan.FromSeconds(10));
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync(Settings());

        Assert.Equal(1, exitCode);
        Assert.Equal([1, 1], merger.MergeCalls.Select(c => c.Pr.Number));
        Assert.Contains("failed — rate limited by GitHub", console.Output);
        Assert.DoesNotContain("insufficient permission", console.Output);
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

        var exitCode = await command.RunAsync(Settings());

        Assert.Equal(0, exitCode);
        Assert.Empty(merger.MergeCalls);
        Assert.Contains("no longer open", console.Output);
    }

    [Fact]
    public async Task RunAsync_NeverWaitsBetweenSuccessfulMergesOnTheSameRepo()
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

        await command.RunAsync(Settings());

        Assert.Equal(2, merger.MergeCalls.Count);
        Assert.Empty(waits);
    }

    [Fact]
    public async Task RunAsync_SelectionScreen_DoesNotThrow_WhenTitleContainsMarkupCharacters()
    {
        var merger = new FakePullRequestMerger();
        var (command, console) = CreateCommand(
            merger,
            SearchResponse(Node(1, "Bump [special]/pkg from 1.0.0 to 1.0.1")));
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync(Settings());

        Assert.Equal(0, exitCode);
        Assert.Contains("[special]/pkg", console.Output);
    }

    [Fact]
    public async Task RunAsync_WhenOnePrsPollTimesOut_ContinuesWithTheRestOfTheBatch()
    {
        var merger = new FakePullRequestMerger { FailForPrNumbers = { 1 } };
        var (command, console) = CreateCommand(
            merger,
            [
                SearchResponse(
                    Node(1, "Bump patch-dep-1 from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND"),
                    Node(2, "Bump patch-dep-2 from 1.0.0 to 1.0.1")),
                ByNumberResponse(1, "Bump patch-dep-1 from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND"),
                ByNumberResponse(1, "Bump patch-dep-1 from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND"),
                ByNumberResponse(2, "Bump patch-dep-2 from 1.0.0 to 1.0.1"),
            ],
            delay: NoOpDelay,
            pollInterval: TimeSpan.FromSeconds(1),
            pollTimeout: TimeSpan.FromSeconds(1));
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync(Settings());

        Assert.Equal(0, exitCode);
        Assert.Equal([1, 2], merger.MergeCalls.Select(c => c.Pr.Number));
        Assert.Contains("skipped", console.Output);
        Assert.Contains("Done: 1 merged, 1 skipped, 0 failed", console.Output);
    }

    [Fact]
    public async Task RunAsync_WithSecurity_NarrowsSelectionScreenToSecurityUpdatePrsOnly()
    {
        var merger = new FakePullRequestMerger();
        var (command, console) = CreateCommand(
            merger,
            SearchResponseWithSecurityAlert(
                securityPrNumber: 1,
                Node(1, "Bump left-pad from 1.0.0 to 1.0.1"),
                Node(2, "Bump right-pad from 1.0.0 to 1.0.1")),
            ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1"));
        console.Input.PushKey(ConsoleKey.Enter);

        await command.RunAsync(Settings(security: true));

        Assert.Contains("left-pad", console.Output);
        Assert.DoesNotContain("right-pad", console.Output);
    }

    [Fact]
    public async Task RunAsync_WithRepo_ScopesDiscoveryAndFetchToThatRepo()
    {
        var merger = new FakePullRequestMerger();
        var source = new FakeRepositorySource(
            new RepositoryInfo("octocat", "sample-repo", IsArchived: false, IsFork: false),
            new RepositoryInfo("octocat", "other-repo", IsArchived: false, IsFork: false));
        var restClient = new RestClient(source);
        var handler = new FakeHttpMessageHandler(
            SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1")),
            ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1"));
        var graphQlClient = new GraphQlClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") });
        var console = new TestConsole().Interactive();
        console.Input.PushKey(ConsoleKey.Enter);
        var command = new MergeCommand(restClient, graphQlClient, merger, console, new FakeAuditLog());

        var exitCode = await command.RunAsync(Settings("sample-repo"));

        Assert.Equal(0, exitCode);
        Assert.False(source.GetOwnedRepositoriesAsyncWasCalled);
        var call = Assert.Single(merger.MergeCalls);
        Assert.Equal(1, call.Pr.Number);
    }

    [Fact]
    public async Task RunAsync_WithUnknownRepo_ThrowsRepositoryNotFoundException()
    {
        var merger = new FakePullRequestMerger();
        var source = new FakeRepositorySource(
            new RepositoryInfo("octocat", "sample-repo", IsArchived: false, IsFork: false));
        var restClient = new RestClient(source);
        var graphQlClient = new GraphQlClient(new HttpClient(new FakeHttpMessageHandler()) { BaseAddress = new Uri("https://api.github.com/") });
        var console = new TestConsole().Interactive();
        var command = new MergeCommand(restClient, graphQlClient, merger, console, new FakeAuditLog());

        await Assert.ThrowsAsync<RepositoryNotFoundException>(() => command.RunAsync(Settings("does-not-exist")));

        Assert.False(source.GetOwnedRepositoriesAsyncWasCalled);
        Assert.Empty(merger.MergeCalls);
    }

    [Fact]
    public async Task RunAsync_WithYes_SkipsSelectionScreenAndActsOnDefaultSelectedSet()
    {
        var merger = new FakePullRequestMerger();
        var (command, console) = CreateCommand(
            merger,
            SearchResponse(
                Node(1, "Bump patch-dep from 1.0.0 to 1.0.1"),
                Node(2, "Bump major-dep from 1.0.0 to 2.0.0")),
            ByNumberResponse(1, "Bump patch-dep from 1.0.0 to 1.0.1"));

        var exitCode = await command.RunAsync(Settings(yes: true));

        Assert.Equal(0, exitCode);
        var call = Assert.Single(merger.MergeCalls);
        Assert.Equal(1, call.Pr.Number);
    }

    [Fact]
    public async Task RunAsync_WithDryRun_ShowsSelectionAndSummaryButMergesNothing()
    {
        var merger = new FakePullRequestMerger();
        var (command, console) = CreateCommand(
            merger,
            SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1")),
            ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1"));
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync(Settings(dryRun: true));

        Assert.Equal(0, exitCode);
        Assert.Empty(merger.MergeCalls);
        Assert.Contains("merged", console.Output);
        Assert.Contains("dry run — no changes made", console.Output);
    }

    [Fact]
    public async Task RunAsync_WithYesAndJson_ActsWithoutPromptingAndPrintsStructuredResults()
    {
        var merger = new FakePullRequestMerger();
        var (command, console) = CreateCommand(
            merger,
            SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1")),
            ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1"));

        var exitCode = await command.RunAsync(Settings(yes: true, json: true));

        Assert.Equal(0, exitCode);
        var call = Assert.Single(merger.MergeCalls);
        Assert.Equal(1, call.Pr.Number);
        Assert.Contains("\"outcome\": \"merged\"", console.Output);
        Assert.Contains("\"dryRun\": false", console.Output);
        Assert.DoesNotContain("Merging selected PRs", console.Output);
    }

    [Fact]
    public async Task RunAsync_WithDryRunAndYes_ActsOnDefaultSetWithZeroMutations()
    {
        var merger = new FakePullRequestMerger();
        var (command, console) = CreateCommand(
            merger,
            SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1")),
            ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1"));

        var exitCode = await command.RunAsync(Settings(dryRun: true, yes: true));

        Assert.Equal(0, exitCode);
        Assert.Empty(merger.MergeCalls);
        Assert.Contains("Done: 1 merged, 0 skipped, 0 failed", console.Output);
    }

    [Fact]
    public async Task RunAsync_WithVerbose_PrintsDiscoveryFetchAndRefetchDiagnostics()
    {
        var merger = new FakePullRequestMerger();
        var (command, console) = CreateCommand(
            merger,
            SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1")),
            ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1"));
        console.Input.PushKey(ConsoleKey.Enter);

        await command.RunAsync(Settings(verbose: true));

        Assert.Contains("Discovered 1 repo(s)", console.Output);
        Assert.Contains("Fetched 1 Dependabot PR(s)", console.Output);
        Assert.Contains("Refetching state for octocat/sample-repo#1", console.Output);
    }

    [Fact]
    public async Task RunAsync_WithoutVerbose_PrintsNoDiagnostics()
    {
        var merger = new FakePullRequestMerger();
        var (command, console) = CreateCommand(
            merger,
            SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1")),
            ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1"));
        console.Input.PushKey(ConsoleKey.Enter);

        await command.RunAsync(Settings());

        Assert.DoesNotContain("Discovered", console.Output);
        Assert.DoesNotContain("Refetching state", console.Output);
    }

    [Fact]
    public async Task RunAsync_WithVerboseAndJson_SuppressesDiagnosticsToKeepOutputStructured()
    {
        var merger = new FakePullRequestMerger();
        var (command, console) = CreateCommand(
            merger,
            SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1")),
            ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1"));

        await command.RunAsync(Settings(yes: true, json: true, verbose: true));

        Assert.DoesNotContain("Discovered", console.Output);
        Assert.DoesNotContain("Refetching state", console.Output);
    }

    [Fact]
    public async Task RunAsync_WithJsonAndNoPrsReady_PrintsEmptyStructuredResults()
    {
        var merger = new FakePullRequestMerger();
        var (command, console) = CreateCommand(merger, SearchResponse());

        var exitCode = await command.RunAsync(Settings(json: true));

        Assert.Equal(0, exitCode);
        Assert.Contains("\"results\": []", console.Output);
    }

    [Fact]
    public async Task RunAsync_WithJson_WhenVulnerabilityAlertsAreTruncated_IncludesReposInJsonOutput()
    {
        var merger = new FakePullRequestMerger();
        var (command, console) = CreateCommand(merger, EmptySearchResponseWithTruncatedSecurityAlerts());

        var exitCode = await command.RunAsync(Settings(json: true));

        Assert.Equal(0, exitCode);
        Assert.Contains("\"reposWithTruncatedSecurityAlerts\": [", console.Output);
        Assert.Contains("\"sample-repo\"", console.Output);
    }

    [Fact]
    public async Task RunAsync_WithNoOverride_UsesThePollIntervalAndTimeoutFromTheInjectedConfig()
    {
        var merger = new FakePullRequestMerger { FailForPrNumbers = { 1 } };
        var console = new TestConsole().Interactive();
        var restClient = new RestClient(new FakeRepositorySource(
            new RepositoryInfo("octocat", "sample-repo", IsArchived: false, IsFork: false)));
        var handler = new FakeHttpMessageHandler(
            SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND")),
            ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND"),
            ByNumberResponse(1, "Bump left-pad from 1.0.0 to 1.0.1", mergeStateStatus: "BEHIND"));
        var graphQlClient = new GraphQlClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") });
        var config = new ChorectlConfig { MergePollIntervalSeconds = 3, MergePollTimeoutSeconds = 3 };
        var command = new MergeCommand(restClient, graphQlClient, merger, console, new FakeAuditLog(), config, NoOpDelay);
        console.Input.PushKey(ConsoleKey.Enter);

        await command.RunAsync(Settings());

        Assert.Contains("still not mergeable after 3s", console.Output);
    }

    private static Task NoOpDelay(TimeSpan wait, CancellationToken cancellationToken) => Task.CompletedTask;

    private static (MergeCommand Command, TestConsole Console) CreateCommand(
        FakePullRequestMerger merger,
        params string[] graphQlResponses) =>
        CreateCommand(merger, graphQlResponses, delay: null);

    private static (MergeCommand Command, TestConsole Console) CreateCommand(
        FakePullRequestMerger merger,
        string[] graphQlResponses,
        Func<TimeSpan, CancellationToken, Task>? delay,
        TimeSpan? pollInterval = null,
        TimeSpan? pollTimeout = null,
        FakeAuditLog? auditLog = null,
        DefaultSelectConfig? defaultSelect = null)
    {
        var console = new TestConsole().Interactive();
        var restClient = new RestClient(new FakeRepositorySource(
            new RepositoryInfo("octocat", "sample-repo", IsArchived: false, IsFork: false)));
        var handler = new FakeHttpMessageHandler(graphQlResponses);
        var graphQlClient = new GraphQlClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") });
        var config = pollInterval is null && pollTimeout is null && defaultSelect is null
            ? null
            : new ChorectlConfig
            {
                MergePollIntervalSeconds = (int)(pollInterval ?? TimeSpan.FromSeconds(15)).TotalSeconds,
                MergePollTimeoutSeconds = (int)(pollTimeout ?? TimeSpan.FromSeconds(120)).TotalSeconds,
                DefaultSelect = defaultSelect ?? new DefaultSelectConfig(),
            };
        var command = new MergeCommand(restClient, graphQlClient, merger, console, auditLog ?? new FakeAuditLog(), config, delay);
        return (command, console);
    }

    private static MergeCommand.Settings Settings(
        string? repo = null, bool security = false, bool dryRun = false, bool yes = false, bool json = false, bool verbose = false) =>
        new() { Repo = repo, Security = security, DryRun = dryRun, Yes = yes, Json = json, Verbose = verbose };

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

    private static string EmptySearchResponseWithTruncatedSecurityAlerts() => $$"""
        {
          "data": {
            "search": {
              "pageInfo": { "hasNextPage": false, "endCursor": null },
              "nodes": []
            },
            "repo0": {
              "name": "sample-repo",
              "vulnerabilityAlerts": {
                "pageInfo": { "hasNextPage": true },
                "nodes": []
              }
            }
          }
        }
        """;

    private static string SearchResponseWithSecurityAlert(int securityPrNumber, params string[] nodes) => $$"""
        {
          "data": {
            "search": {
              "pageInfo": { "hasNextPage": false, "endCursor": null },
              "nodes": [ {{string.Join(",", nodes)}} ]
            },
            "repo0": {
              "name": "sample-repo",
              "vulnerabilityAlerts": {
                "nodes": [
                  { "dependabotUpdate": { "pullRequest": { "number": {{securityPrNumber}} } } }
                ]
              }
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

    private static string ByNumberResponse(int number, string title, string mergeStateStatus = "CLEAN", string ci = "SUCCESS", string? review = null, string state = "OPEN", string? body = null) => $$"""
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
                "body": {{(body is null ? "null" : $"\"{body}\"")}},
                "repository": { "name": "sample-repo" },
                "labels": { "nodes": [] },
                "commits": { "nodes": [ { "commit": { "statusCheckRollup": { "state": "{{ci}}" } } } ] }
              }
            }
          }
        }
        """;
}
