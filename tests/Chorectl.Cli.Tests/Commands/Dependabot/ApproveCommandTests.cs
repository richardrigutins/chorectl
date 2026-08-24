using Chorectl.Cli.Commands.Dependabot;
using Chorectl.Core.GitHub;
using Spectre.Console.Testing;

namespace Chorectl.Cli.Tests.Commands.Dependabot;

public class ApproveCommandTests
{
    [Fact]
    public async Task RunAsync_WhenPrIsApproved_RecordsAnApprovedAuditEntry()
    {
        var approver = new FakePullRequestApprover();
        var auditLog = new FakeAuditLog();
        var (command, console) = CreateCommand(
            approver,
            [SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1", review: "REVIEW_REQUIRED"))],
            auditLog: auditLog);
        console.Input.PushKey(ConsoleKey.Enter);

        await command.RunAsync(Settings());

        var entry = Assert.Single(auditLog.Entries);
        Assert.Equal("sample-repo", entry.Repo);
        Assert.Equal(1, entry.PrNumber);
        Assert.Equal("approved", entry.Action);
        Assert.Null(entry.Reason);
    }

    [Fact]
    public async Task RunAsync_WhenApproveFails_RecordsAFailedAuditEntryWithReason()
    {
        var approver = new FakePullRequestApprover { FailWithAuthErrorForPrNumbers = { 1 } };
        var auditLog = new FakeAuditLog();
        var (command, console) = CreateCommand(
            approver,
            [SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1", review: "REVIEW_REQUIRED"))],
            auditLog: auditLog);
        console.Input.PushKey(ConsoleKey.Enter);

        await command.RunAsync(Settings());

        var entry = Assert.Single(auditLog.Entries);
        Assert.Equal("failed", entry.Action);
        Assert.Contains("insufficient permission to review", entry.Reason);
    }

    [Fact]
    public async Task RunAsync_WithDryRun_RecordsNoAuditEntries()
    {
        var approver = new FakePullRequestApprover();
        var auditLog = new FakeAuditLog();
        var (command, console) = CreateCommand(
            approver,
            [SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1", review: "REVIEW_REQUIRED"))],
            auditLog: auditLog);
        console.Input.PushKey(ConsoleKey.Enter);

        await command.RunAsync(Settings(dryRun: true));

        Assert.Empty(auditLog.Entries);
    }

    [Fact]
    public async Task RunAsync_WithNoPrsNeedingApproval_PrintsMessageAndApprovesNothing()
    {
        var approver = new FakePullRequestApprover();
        var (command, console) = CreateCommand(approver, SearchResponse());

        var exitCode = await command.RunAsync(Settings());

        Assert.Equal(0, exitCode);
        Assert.Contains("No Dependabot PRs need approval.", console.Output);
        Assert.Empty(approver.ApproveCalls);
    }

    [Fact]
    public async Task RunAsync_OnlyShowsPrsNeedingApproval()
    {
        var approver = new FakePullRequestApprover();
        var (command, console) = CreateCommand(
            approver,
            SearchResponse(
                Node(1, "Bump left-pad from 1.0.0 to 1.0.1", review: "REVIEW_REQUIRED"),
                Node(2, "Bump right-pad from 1.0.0 to 1.0.1", review: "APPROVED")));
        console.Input.PushKey(ConsoleKey.Enter);

        await command.RunAsync(Settings());

        Assert.Contains("left-pad", console.Output);
        Assert.DoesNotContain("right-pad", console.Output);
    }

    [Fact]
    public async Task RunAsync_ReposWhereReviewIsntRequiredNeverAppear()
    {
        var approver = new FakePullRequestApprover();
        var (command, console) = CreateCommand(
            approver,
            SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1", review: null)));

        var exitCode = await command.RunAsync(Settings());

        Assert.Equal(0, exitCode);
        Assert.Contains("No Dependabot PRs need approval.", console.Output);
        Assert.Empty(approver.ApproveCalls);
    }

    [Fact]
    public async Task RunAsync_ConfirmingDefaults_ApprovesEveryPr_SinceAllArePreselected()
    {
        var approver = new FakePullRequestApprover();
        var (command, console) = CreateCommand(
            approver,
            SearchResponse(
                Node(1, "Bump left-pad from 1.0.0 to 1.0.1", review: "REVIEW_REQUIRED"),
                Node(2, "Bump right-pad from 1.0.0 to 1.0.1", review: "REVIEW_REQUIRED")));
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync(Settings());

        Assert.Equal(0, exitCode);
        Assert.Equal([1, 2], approver.ApproveCalls.Select(c => c.Pr.Number).Order());
        Assert.Contains("approved", console.Output);
        Assert.Contains("Done: 2 approved, 0 failed", console.Output);
    }

    [Fact]
    public async Task RunAsync_WithNothingSelected_PrintsMessageAndApprovesNothing()
    {
        var approver = new FakePullRequestApprover();
        var (command, console) = CreateCommand(
            approver,
            SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1", review: "REVIEW_REQUIRED")));
        // Cursor starts on the repo group header (pre-checked); space deselects everything under it.
        console.Input.PushKey(ConsoleKey.Spacebar);
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync(Settings());

        Assert.Equal(0, exitCode);
        Assert.Contains("No PRs selected. Nothing approved.", console.Output);
        Assert.Empty(approver.ApproveCalls);
    }

    [Fact]
    public async Task RunAsync_WhenApproveFailsWithInsufficientPermission_ReportsFailureAndNonZeroExitCode()
    {
        var approver = new FakePullRequestApprover { FailWithAuthErrorForPrNumbers = { 1 } };
        var (command, console) = CreateCommand(
            approver,
            SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1", review: "REVIEW_REQUIRED")));
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync(Settings());

        Assert.Equal(1, exitCode);
        Assert.Contains("failed — insufficient permission to review", console.Output);
        Assert.Contains("Done: 0 approved, 1 failed", console.Output);
    }

    [Fact]
    public async Task RunAsync_WhenApproveFailsWithRateLimit_ReportsFailureWithoutInsufficientPermissionMessage()
    {
        var approver = new FakePullRequestApprover { FailWithRateLimitErrorForPrNumbers = { 1 } };
        var (command, console) = CreateCommand(
            approver,
            SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1", review: "REVIEW_REQUIRED")));
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync(Settings());

        Assert.Equal(1, exitCode);
        Assert.Contains("failed — rate limited by GitHub", console.Output);
        Assert.DoesNotContain("insufficient permission", console.Output);
    }

    [Fact]
    public async Task RunAsync_WhenApproveFailsWithUnrecognizedError_SkipsWithRawErrorSurfaced()
    {
        var approver = new FakePullRequestApprover
        {
            FailWithUnexpectedErrorForPrNumbers = { 1 },
            FailureMessage = "422 Validation Failed",
        };
        var (command, console) = CreateCommand(
            approver,
            SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1", review: "REVIEW_REQUIRED")));
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync(Settings());

        Assert.Equal(1, exitCode);
        Assert.Contains("failed — unexpected error, likely a tool bug", console.Output);
        Assert.Contains("422 Validation Failed", console.Output);
    }

    [Fact]
    public async Task RunAsync_WhenOnePrFails_ContinuesWithTheRestOfTheBatch()
    {
        var approver = new FakePullRequestApprover { FailWithAuthErrorForPrNumbers = { 1 } };
        var (command, console) = CreateCommand(
            approver,
            SearchResponse(
                Node(1, "Bump left-pad from 1.0.0 to 1.0.1", review: "REVIEW_REQUIRED"),
                Node(2, "Bump right-pad from 1.0.0 to 1.0.1", review: "REVIEW_REQUIRED")));
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync(Settings());

        Assert.Equal(1, exitCode);
        Assert.Equal([1, 2], approver.ApproveCalls.Select(c => c.Pr.Number).Order());
        Assert.Contains("Done: 1 approved, 1 failed", console.Output);
    }

    [Fact]
    public async Task RunAsync_WithVerbose_PrintsDiscoveryAndFetchDiagnostics()
    {
        var approver = new FakePullRequestApprover();
        var (command, console) = CreateCommand(
            approver,
            [SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1", review: "REVIEW_REQUIRED"))]);
        console.Input.PushKey(ConsoleKey.Enter);

        await command.RunAsync(Settings(verbose: true));

        Assert.Contains("Discovered 1 repo(s)", console.Output);
        Assert.Contains("Fetched 1 Dependabot PR(s)", console.Output);
    }

    [Fact]
    public async Task RunAsync_WithoutVerbose_PrintsNoDiagnostics()
    {
        var approver = new FakePullRequestApprover();
        var (command, console) = CreateCommand(
            approver,
            [SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1", review: "REVIEW_REQUIRED"))]);
        console.Input.PushKey(ConsoleKey.Enter);

        await command.RunAsync(Settings());

        Assert.DoesNotContain("Discovered", console.Output);
        Assert.DoesNotContain("Fetched", console.Output);
    }

    [Fact]
    public async Task RunAsync_WithVerboseAndJson_SuppressesDiagnosticsToKeepOutputStructured()
    {
        var approver = new FakePullRequestApprover();
        var (command, console) = CreateCommand(
            approver,
            [SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1", review: "REVIEW_REQUIRED"))]);

        await command.RunAsync(Settings(yes: true, json: true, verbose: true));

        Assert.DoesNotContain("Discovered", console.Output);
        Assert.DoesNotContain("Fetched", console.Output);
    }

    [Fact]
    public async Task RunAsync_WithSecurity_FiltersToSecurityUpdatePrsThatNeedApproval()
    {
        var approver = new FakePullRequestApprover();
        var (command, console) = CreateCommand(
            approver,
            [SearchResponseWithSecurityAlert(
                securityPrNumber: 1,
                Node(1, "Bump left-pad from 1.0.0 to 1.0.1", review: "REVIEW_REQUIRED"),
                Node(2, "Bump right-pad from 1.0.0 to 1.0.1", review: "REVIEW_REQUIRED"))]);
        console.Input.PushKey(ConsoleKey.Enter);

        await command.RunAsync(Settings(security: true));

        Assert.Contains("left-pad", console.Output);
        Assert.DoesNotContain("right-pad", console.Output);
    }

    [Fact]
    public async Task RunAsync_WithRepo_ScopesDiscoveryAndFetchToThatRepo()
    {
        var approver = new FakePullRequestApprover();
        var source = new FakeRepositorySource(
            new RepositoryInfo("octocat", "sample-repo", IsArchived: false, IsFork: false),
            new RepositoryInfo("octocat", "other-repo", IsArchived: false, IsFork: false));
        var restClient = new RestClient(source);
        var handler = new FakeHttpMessageHandler(
            SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1", review: "REVIEW_REQUIRED")));
        var graphQlClient = new GraphQlClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") });
        var console = new TestConsole().Interactive();
        console.Input.PushKey(ConsoleKey.Enter);
        var command = new ApproveCommand(restClient, graphQlClient, approver, console, new FakeAuditLog());

        var exitCode = await command.RunAsync(Settings("sample-repo"));

        Assert.Equal(0, exitCode);
        Assert.False(source.GetOwnedRepositoriesAsyncWasCalled);
        var call = Assert.Single(approver.ApproveCalls);
        Assert.Equal(1, call.Pr.Number);
    }

    [Fact]
    public async Task RunAsync_WithUnknownRepo_ThrowsRepositoryNotFoundException()
    {
        var approver = new FakePullRequestApprover();
        var source = new FakeRepositorySource(
            new RepositoryInfo("octocat", "sample-repo", IsArchived: false, IsFork: false));
        var restClient = new RestClient(source);
        var graphQlClient = new GraphQlClient(new HttpClient(new FakeHttpMessageHandler()) { BaseAddress = new Uri("https://api.github.com/") });
        var console = new TestConsole().Interactive();
        var command = new ApproveCommand(restClient, graphQlClient, approver, console, new FakeAuditLog());

        await Assert.ThrowsAsync<RepositoryNotFoundException>(() => command.RunAsync(Settings("does-not-exist")));

        Assert.False(source.GetOwnedRepositoriesAsyncWasCalled);
        Assert.Empty(approver.ApproveCalls);
    }

    [Fact]
    public async Task RunAsync_WithYes_SkipsSelectionScreenAndApprovesAll()
    {
        var approver = new FakePullRequestApprover();
        var (command, console) = CreateCommand(
            approver,
            SearchResponse(
                Node(1, "Bump left-pad from 1.0.0 to 1.0.1", review: "REVIEW_REQUIRED"),
                Node(2, "Bump right-pad from 1.0.0 to 1.0.1", review: "REVIEW_REQUIRED")));

        var exitCode = await command.RunAsync(Settings(yes: true));

        Assert.Equal(0, exitCode);
        Assert.Equal([1, 2], approver.ApproveCalls.Select(c => c.Pr.Number).Order());
    }

    [Fact]
    public async Task RunAsync_WithDryRun_ShowsSummaryButApprovesNothing()
    {
        var approver = new FakePullRequestApprover();
        var (command, console) = CreateCommand(
            approver,
            SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1", review: "REVIEW_REQUIRED")));
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync(Settings(dryRun: true));

        Assert.Equal(0, exitCode);
        Assert.Empty(approver.ApproveCalls);
        Assert.Contains("approved", console.Output);
        Assert.Contains("dry run — no changes made", console.Output);
    }

    [Fact]
    public async Task RunAsync_WithJson_PrintsStructuredResultsWithoutPrompting()
    {
        var approver = new FakePullRequestApprover();
        var (command, console) = CreateCommand(
            approver,
            SearchResponse(Node(1, "Bump left-pad from 1.0.0 to 1.0.1", review: "REVIEW_REQUIRED")));

        var exitCode = await command.RunAsync(Settings(json: true));

        Assert.Equal(0, exitCode);
        var call = Assert.Single(approver.ApproveCalls);
        Assert.Equal(1, call.Pr.Number);
        Assert.Contains("\"outcome\": \"approved\"", console.Output);
    }

    private static (ApproveCommand Command, TestConsole Console) CreateCommand(
        FakePullRequestApprover approver,
        params string[] graphQlResponses) =>
        CreateCommand(approver, graphQlResponses, auditLog: null);

    private static (ApproveCommand Command, TestConsole Console) CreateCommand(
        FakePullRequestApprover approver,
        string[] graphQlResponses,
        FakeAuditLog? auditLog)
    {
        var console = new TestConsole().Interactive();
        var restClient = new RestClient(new FakeRepositorySource(
            new RepositoryInfo("octocat", "sample-repo", IsArchived: false, IsFork: false)));
        var handler = new FakeHttpMessageHandler(graphQlResponses);
        var graphQlClient = new GraphQlClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") });
        var command = new ApproveCommand(restClient, graphQlClient, approver, console, auditLog ?? new FakeAuditLog());
        return (command, console);
    }

    private static ApproveCommand.Settings Settings(
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
          "body": null,
          "repository": { "name": "sample-repo" },
          "labels": { "nodes": [] },
          "commits": { "nodes": [ { "commit": { "statusCheckRollup": { "state": "{{ci}}" } } } ] }
        }
        """;
}
