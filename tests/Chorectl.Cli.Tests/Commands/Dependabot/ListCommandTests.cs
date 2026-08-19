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

        await command.RunAsync();

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

        var exitCode = await command.RunAsync();

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

        var exitCode = await command.RunAsync("sample-repo");

        Assert.Equal(0, exitCode);
        Assert.False(source.GetOwnedRepositoriesAsyncWasCalled);
        Assert.Contains("sample-repo", console.Output);
        Assert.Contains("firebase-tools", console.Output);
    }

    [Fact]
    public async Task RunAsync_WithUnknownRepo_PrintsEmptyState()
    {
        var console = new TestConsole();
        var source = new FakeRepositorySource(
            new RepositoryInfo("octocat", "sample-repo", IsArchived: false, IsFork: false));
        var restClient = new RestClient(source);
        var graphQlClient = new GraphQlClient(new HttpClient(new FakeHttpMessageHandler()) { BaseAddress = new Uri("https://api.github.com/") });
        var command = new ListCommand(restClient, graphQlClient, console);

        var exitCode = await command.RunAsync("does-not-exist");

        Assert.Equal(0, exitCode);
        Assert.False(source.GetOwnedRepositoriesAsyncWasCalled);
        Assert.Contains("No open Dependabot PRs found.", console.Output);
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

        await command.RunAsync();

        Assert.Contains("No open Dependabot PRs found.", console.Output);
    }

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
}
