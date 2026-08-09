using Chorectl.Core.Domain.Dependabot;
using Chorectl.Core.GitHub;

namespace Chorectl.Core.Tests.GitHub;

public class GraphQlClientTests
{
    private static readonly string FixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "dependabot-pr-search-response.json");

    [Fact]
    public async Task FetchDependabotPrsAsync_WithNoRepos_ReturnsEmptyWithoutSendingARequest()
    {
        var handler = new FakeHttpMessageHandler();
        var client = CreateClient(handler);

        var prs = await client.FetchDependabotPrsAsync([]);

        Assert.Empty(prs);
        Assert.Empty(handler.RequestBodies);
    }

    [Fact]
    public async Task FetchDependabotPrsAsync_MapsFieldsFromTheGraphQlResponse()
    {
        var handler = new FakeHttpMessageHandler(await File.ReadAllTextAsync(FixturePath));
        var client = CreateClient(handler);

        var prs = await client.FetchDependabotPrsAsync([new RepositoryInfo("octocat", "sample-repo", IsArchived: false, IsFork: false)]);

        var pr = Assert.Single(prs);
        Assert.Equal("sample-repo", pr.Repo);
        Assert.Equal(42, pr.Number);
        Assert.Equal("Bump firebase-tools from 11.2.0 to 11.3.1", pr.Title);
        Assert.Equal("https://github.com/octocat/sample-repo/pull/42", pr.Url);
        Assert.Equal("dependabot/npm_and_yarn/firebase-tools-11.3.1", pr.HeadRefName);
        Assert.False(pr.IsDraft);
        Assert.Equal(DateTimeOffset.Parse("2026-08-01T12:00:00Z"), pr.UpdatedAt);
        Assert.Equal(CiStatus.Passing, pr.Ci);
        Assert.Equal(ReviewStatus.Approved, pr.Review);
        Assert.Equal("CLEAN", pr.MergeStateStatus);
        Assert.Equal(SemverLevel.Minor, pr.SemverLevel);
    }

    [Theory]
    [InlineData("SUCCESS", CiStatus.Passing)]
    [InlineData("FAILURE", CiStatus.Failing)]
    [InlineData("ERROR", CiStatus.Failing)]
    [InlineData("PENDING", CiStatus.Pending)]
    [InlineData("EXPECTED", CiStatus.Pending)]
    public async Task FetchDependabotPrsAsync_MapsStatusCheckRollupStateToCiStatus(string rollupState, CiStatus expected)
    {
        var handler = new FakeHttpMessageHandler(SingleNodeResponse($$"""
            "reviewDecision": null,
            "mergeStateStatus": "CLEAN",
            "commits": { "nodes": [ { "commit": { "statusCheckRollup": { "state": "{{rollupState}}" } } } ] }
            """));
        var client = CreateClient(handler);

        var prs = await client.FetchDependabotPrsAsync([new RepositoryInfo("octocat", "repo", IsArchived: false, IsFork: false)]);

        Assert.Equal(expected, Assert.Single(prs).Ci);
    }

    [Fact]
    public async Task FetchDependabotPrsAsync_WithNoCommits_MapsToNoChecks()
    {
        var handler = new FakeHttpMessageHandler(SingleNodeResponse("""
            "reviewDecision": null,
            "mergeStateStatus": "CLEAN",
            "commits": { "nodes": [] }
            """));
        var client = CreateClient(handler);

        var prs = await client.FetchDependabotPrsAsync([new RepositoryInfo("octocat", "repo", IsArchived: false, IsFork: false)]);

        Assert.Equal(CiStatus.NoChecks, Assert.Single(prs).Ci);
    }

    [Theory]
    [InlineData("APPROVED", ReviewStatus.Approved)]
    [InlineData("REVIEW_REQUIRED", ReviewStatus.ReviewRequired)]
    [InlineData("CHANGES_REQUESTED", ReviewStatus.ReviewRequired)]
    [InlineData(null, ReviewStatus.NotRequired)]
    public async Task FetchDependabotPrsAsync_MapsReviewDecisionToReviewStatus(string? reviewDecision, ReviewStatus expected)
    {
        var reviewDecisionJson = reviewDecision is null ? "null" : $"\"{reviewDecision}\"";
        var handler = new FakeHttpMessageHandler(SingleNodeResponse($$"""
            "reviewDecision": {{reviewDecisionJson}},
            "mergeStateStatus": "CLEAN",
            "commits": { "nodes": [] }
            """));
        var client = CreateClient(handler);

        var prs = await client.FetchDependabotPrsAsync([new RepositoryInfo("octocat", "repo", IsArchived: false, IsFork: false)]);

        Assert.Equal(expected, Assert.Single(prs).Review);
    }

    [Fact]
    public async Task FetchDependabotPrsAsync_FollowsPaginationWithinABatch()
    {
        var firstPage = SingleNodeResponse("""
            "reviewDecision": "APPROVED",
            "mergeStateStatus": "CLEAN",
            "commits": { "nodes": [] }
            """, number: 1, hasNextPage: true, endCursor: "cursor-1");
        var secondPage = SingleNodeResponse("""
            "reviewDecision": "APPROVED",
            "mergeStateStatus": "CLEAN",
            "commits": { "nodes": [] }
            """, number: 2, hasNextPage: false);

        var handler = new FakeHttpMessageHandler(firstPage, secondPage);
        var client = CreateClient(handler);

        var prs = await client.FetchDependabotPrsAsync([new RepositoryInfo("octocat", "repo", IsArchived: false, IsFork: false)]);

        Assert.Equal(2, handler.RequestBodies.Count);
        Assert.Contains("cursor-1", handler.RequestBodies[1]);
        Assert.Equal([1, 2], prs.Select(p => p.Number));
    }

    [Fact]
    public async Task FetchDependabotPrsAsync_BatchesManyReposIntoFewerRequestsThanRepos()
    {
        var repos = Enumerable.Range(0, 50)
            .Select(i => new RepositoryInfo("richardrigutins", $"some-long-repo-name-{i:00}", IsArchived: false, IsFork: false))
            .ToList();
        var emptyResponse = """{ "data": { "search": { "pageInfo": { "hasNextPage": false, "endCursor": null }, "nodes": [] } } }""";
        var handler = new FakeHttpMessageHandler(Enumerable.Repeat(emptyResponse, repos.Count).ToArray());
        var client = CreateClient(handler);

        await client.FetchDependabotPrsAsync(repos);

        Assert.True(handler.RequestBodies.Count > 1, "expected repos to be split across more than one request");
        Assert.True(handler.RequestBodies.Count < repos.Count, "expected repos to be batched into fewer requests than repos");
    }

    [Fact]
    public async Task FetchDependabotPrsAsync_WhenTheApiReturnsErrors_Throws()
    {
        var handler = new FakeHttpMessageHandler("""{ "data": null, "errors": [ { "message": "something went wrong" } ] }""");
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.FetchDependabotPrsAsync([new RepositoryInfo("octocat", "repo", IsArchived: false, IsFork: false)]));
        Assert.Contains("something went wrong", exception.Message);
    }

    private static GraphQlClient CreateClient(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        return new GraphQlClient(httpClient);
    }

    private static string SingleNodeResponse(string extraFields, int number = 1, bool hasNextPage = false, string? endCursor = null) => $$"""
        {
          "data": {
            "search": {
              "pageInfo": { "hasNextPage": {{(hasNextPage ? "true" : "false")}}, "endCursor": {{(endCursor is null ? "null" : $"\"{endCursor}\"")}} },
              "nodes": [
                {
                  "number": {{number}},
                  "title": "Bump some-dependency from 1.0.0 to 1.1.0",
                  "url": "https://github.com/octocat/repo/pull/{{number}}",
                  "headRefName": "dependabot/npm_and_yarn/some-dependency-1.1.0",
                  "isDraft": false,
                  "updatedAt": "2026-08-01T12:00:00Z",
                  "repository": { "name": "repo" },
                  "labels": { "nodes": [] },
                  {{extraFields}}
                }
              ]
            }
          }
        }
        """;
}
