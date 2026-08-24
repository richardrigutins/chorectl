using Chorectl.Cli.Commands.Dependabot;
using Chorectl.Core.GitHub;
using Spectre.Console.Testing;

namespace Chorectl.Cli.Tests.Commands.Dependabot;

public class ListCommandTests
{
    [Fact]
    public async Task ExecuteAsync_WithNoRepos_PrintsEmptyState()
    {
        var console = new TestConsole();
        var restClient = new RestClient(new FakeRepositorySource());
        var graphQlClient = new GraphQlClient(new HttpClient(new FakeHttpMessageHandler()) { BaseAddress = new Uri("https://api.github.com/") });
        var command = new ListCommand(restClient, graphQlClient, console);

        await command.RunAsync(Settings());

        Assert.Contains("No open Dependabot PRs found.", console.Output);
    }

    [Fact]
    public async Task ExecuteAsync_DiscoversReposAndFetchesAndRendersDependabotPrs()
    {
        var console = new TestConsole();
        var restClient = new RestClient(new FakeRepositorySource(
            new RepositoryInfo("octocat", "sample-repo", IsArchived: false, IsFork: false)));
        var handler = new FakeHttpMessageHandler(SingleNodeResponse());
        var graphQlClient = new GraphQlClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") });
        var command = new ListCommand(restClient, graphQlClient, console);

        var exitCode = await command.RunAsync(Settings());

        Assert.Equal(0, exitCode);
        Assert.Contains("sample-repo", console.Output);
        Assert.Contains("firebase-tools", console.Output);
    }

    [Fact]
    public async Task RunAsync_WithRepo_ScopesDiscoveryAndFetchToThatRepo()
    {
        var console = new TestConsole();
        var source = new FakeRepositorySource(
            new RepositoryInfo("octocat", "sample-repo", IsArchived: false, IsFork: false),
            new RepositoryInfo("octocat", "other-repo", IsArchived: false, IsFork: false));
        var restClient = new RestClient(source);
        var handler = new FakeHttpMessageHandler(SingleNodeResponse());
        var graphQlClient = new GraphQlClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") });
        var command = new ListCommand(restClient, graphQlClient, console);

        var exitCode = await command.RunAsync(Settings("sample-repo"));

        Assert.Equal(0, exitCode);
        Assert.False(source.GetOwnedRepositoriesAsyncWasCalled);
        Assert.Contains("sample-repo", console.Output);
        Assert.Contains("firebase-tools", console.Output);
    }

    [Fact]
    public async Task RunAsync_WithJson_PrintsStructuredJsonInsteadOfTable()
    {
        var console = new TestConsole();
        var restClient = new RestClient(new FakeRepositorySource(
            new RepositoryInfo("octocat", "sample-repo", IsArchived: false, IsFork: false)));
        var handler = new FakeHttpMessageHandler(SingleNodeResponse());
        var graphQlClient = new GraphQlClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") });
        var command = new ListCommand(restClient, graphQlClient, console);

        var exitCode = await command.RunAsync(Settings(json: true));

        Assert.Equal(0, exitCode);
        Assert.Contains("\"number\": 42", console.Output);
        Assert.Contains("\"dependencyName\": \"firebase-tools\"", console.Output);
        Assert.DoesNotContain("REPO", console.Output);
    }

    [Fact]
    public async Task RunAsync_WithVerbose_PrintsDiscoveryAndFetchDiagnostics()
    {
        var console = new TestConsole();
        var restClient = new RestClient(new FakeRepositorySource(
            new RepositoryInfo("octocat", "sample-repo", IsArchived: false, IsFork: false)));
        var handler = new FakeHttpMessageHandler(SingleNodeResponse());
        var graphQlClient = new GraphQlClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") });
        var command = new ListCommand(restClient, graphQlClient, console);

        await command.RunAsync(Settings(verbose: true));

        Assert.Contains("Discovered 1 repo(s)", console.Output);
        Assert.Contains("Fetched 1 Dependabot PR(s)", console.Output);
    }

    [Fact]
    public async Task RunAsync_WithoutVerbose_PrintsNoDiagnostics()
    {
        var console = new TestConsole();
        var restClient = new RestClient(new FakeRepositorySource(
            new RepositoryInfo("octocat", "sample-repo", IsArchived: false, IsFork: false)));
        var handler = new FakeHttpMessageHandler(SingleNodeResponse());
        var graphQlClient = new GraphQlClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") });
        var command = new ListCommand(restClient, graphQlClient, console);

        await command.RunAsync(Settings());

        Assert.DoesNotContain("Discovered", console.Output);
        Assert.DoesNotContain("Fetched", console.Output);
    }

    [Fact]
    public async Task RunAsync_WithVerboseAndJson_SuppressesDiagnosticsToKeepOutputStructured()
    {
        var console = new TestConsole();
        var restClient = new RestClient(new FakeRepositorySource(
            new RepositoryInfo("octocat", "sample-repo", IsArchived: false, IsFork: false)));
        var handler = new FakeHttpMessageHandler(SingleNodeResponse());
        var graphQlClient = new GraphQlClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") });
        var command = new ListCommand(restClient, graphQlClient, console);

        await command.RunAsync(Settings(verbose: true, json: true));

        Assert.DoesNotContain("Discovered", console.Output);
        Assert.DoesNotContain("Fetched", console.Output);
    }

    [Fact]
    public async Task RunAsync_WithSecurity_FiltersToSecurityUpdatePrsOnly()
    {
        var console = new TestConsole();
        var restClient = new RestClient(new FakeRepositorySource(
            new RepositoryInfo("octocat", "sample-repo", IsArchived: false, IsFork: false)));
        var handler = new FakeHttpMessageHandler(TwoNodeResponseWithOneSecurityAlert());
        var graphQlClient = new GraphQlClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") });
        var command = new ListCommand(restClient, graphQlClient, console);

        var exitCode = await command.RunAsync(Settings(security: true));

        Assert.Equal(0, exitCode);
        Assert.Contains("firebase-tools", console.Output);
        Assert.DoesNotContain("left-pad", console.Output);
    }

    [Fact]
    public async Task RunAsync_WithoutSecurity_ShowsEveryPr()
    {
        var console = new TestConsole();
        var restClient = new RestClient(new FakeRepositorySource(
            new RepositoryInfo("octocat", "sample-repo", IsArchived: false, IsFork: false)));
        var handler = new FakeHttpMessageHandler(TwoNodeResponseWithOneSecurityAlert());
        var graphQlClient = new GraphQlClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") });
        var command = new ListCommand(restClient, graphQlClient, console);

        await command.RunAsync(Settings());

        Assert.Contains("firebase-tools", console.Output);
        Assert.Contains("left-pad", console.Output);
    }

    [Fact]
    public async Task RunAsync_WithCancelledToken_CancelsTheFetchInsteadOfIgnoringIt()
    {
        var console = new TestConsole();
        var restClient = new RestClient(new FakeRepositorySource(
            new RepositoryInfo("octocat", "sample-repo", IsArchived: false, IsFork: false)));
        var handler = new FakeHttpMessageHandler(SingleNodeResponse());
        var graphQlClient = new GraphQlClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") });
        var command = new ListCommand(restClient, graphQlClient, console);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => command.RunAsync(Settings(), cts.Token));
    }

    [Fact]
    public async Task RunAsync_WithUnknownRepo_ThrowsRepositoryNotFoundException()
    {
        var console = new TestConsole();
        var source = new FakeRepositorySource(
            new RepositoryInfo("octocat", "sample-repo", IsArchived: false, IsFork: false));
        var restClient = new RestClient(source);
        var graphQlClient = new GraphQlClient(new HttpClient(new FakeHttpMessageHandler()) { BaseAddress = new Uri("https://api.github.com/") });
        var command = new ListCommand(restClient, graphQlClient, console);

        await Assert.ThrowsAsync<RepositoryNotFoundException>(() => command.RunAsync(Settings("does-not-exist")));

        Assert.False(source.GetOwnedRepositoriesAsyncWasCalled);
    }

    [Fact]
    public async Task ExecuteAsync_ExcludesArchivedAndForkedRepos()
    {
        var console = new TestConsole();
        var restClient = new RestClient(new FakeRepositorySource(
            new RepositoryInfo("octocat", "archived-repo", IsArchived: true, IsFork: false),
            new RepositoryInfo("octocat", "forked-repo", IsArchived: false, IsFork: true)));
        var handler = new FakeHttpMessageHandler();
        var graphQlClient = new GraphQlClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") });
        var command = new ListCommand(restClient, graphQlClient, console);

        await command.RunAsync(Settings());

        Assert.Contains("No open Dependabot PRs found.", console.Output);
    }

    private static ListCommand.Settings Settings(string? repo = null, bool security = false, bool json = false, bool verbose = false) =>
        new() { Repo = repo, Security = security, Json = json, Verbose = verbose };

    private static string SingleNodeResponse() => """
        {
          "data": {
            "search": {
              "pageInfo": { "hasNextPage": false, "endCursor": null },
              "nodes": [
                {
                  "number": 42,
                  "title": "Bump firebase-tools from 11.2.0 to 11.3.1",
                  "url": "https://github.com/octocat/sample-repo/pull/42",
                  "headRefName": "dependabot/npm_and_yarn/firebase-tools-11.3.1",
                  "isDraft": false,
                  "updatedAt": "2026-08-01T12:00:00Z",
                  "reviewDecision": "APPROVED",
                  "mergeStateStatus": "CLEAN",
                  "repository": { "name": "sample-repo" },
                  "labels": { "nodes": [] },
                  "commits": { "nodes": [ { "commit": { "statusCheckRollup": { "state": "SUCCESS" } } } ] }
                }
              ]
            }
          }
        }
        """;

    private static string TwoNodeResponseWithOneSecurityAlert() => """
        {
          "data": {
            "search": {
              "pageInfo": { "hasNextPage": false, "endCursor": null },
              "nodes": [
                {
                  "number": 42,
                  "title": "Bump firebase-tools from 11.2.0 to 11.3.1",
                  "url": "https://github.com/octocat/sample-repo/pull/42",
                  "headRefName": "dependabot/npm_and_yarn/firebase-tools-11.3.1",
                  "isDraft": false,
                  "updatedAt": "2026-08-01T12:00:00Z",
                  "reviewDecision": "APPROVED",
                  "mergeStateStatus": "CLEAN",
                  "repository": { "name": "sample-repo" },
                  "labels": { "nodes": [] },
                  "commits": { "nodes": [ { "commit": { "statusCheckRollup": { "state": "SUCCESS" } } } ] }
                },
                {
                  "number": 43,
                  "title": "Bump left-pad from 1.0.0 to 1.0.1",
                  "url": "https://github.com/octocat/sample-repo/pull/43",
                  "headRefName": "dependabot/npm_and_yarn/left-pad-1.0.1",
                  "isDraft": false,
                  "updatedAt": "2026-08-01T12:00:00Z",
                  "reviewDecision": "APPROVED",
                  "mergeStateStatus": "CLEAN",
                  "repository": { "name": "sample-repo" },
                  "labels": { "nodes": [] },
                  "commits": { "nodes": [ { "commit": { "statusCheckRollup": { "state": "SUCCESS" } } } ] }
                }
              ]
            },
            "repo0": {
              "name": "sample-repo",
              "vulnerabilityAlerts": {
                "nodes": [
                  { "dependabotUpdate": { "pullRequest": { "number": 42 } } }
                ]
              }
            }
          }
        }
        """;
}
