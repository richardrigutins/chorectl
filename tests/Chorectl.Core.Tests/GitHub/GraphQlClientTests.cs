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
        Assert.Equal("firebase-tools", pr.DependencyName);
        Assert.Equal("11.2.0", pr.FromVersion);
        Assert.Equal("11.3.1", pr.ToVersion);
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

    [Theory]
    [InlineData("Bump the angular group with 2 updates", true)]
    [InlineData("Bump the aws-sdk-go group from 1.2.3 to 1.3.0 in /aws-sdk-go", false)]
    [InlineData("Bump firebase-tools from 11.2.0 to 11.3.1", false)]
    public async Task FetchDependabotPrsAsync_MapsGroupedTitleToIsGrouped(string title, bool expected)
    {
        var handler = new FakeHttpMessageHandler(SingleNodeResponse("""
            "reviewDecision": null,
            "mergeStateStatus": "CLEAN",
            "commits": { "nodes": [] }
            """, title: title));
        var client = CreateClient(handler);

        var prs = await client.FetchDependabotPrsAsync([new RepositoryInfo("octocat", "repo", IsArchived: false, IsFork: false)]);

        Assert.Equal(expected, Assert.Single(prs).IsGrouped);
    }

    [Fact]
    public async Task FetchDependabotPrsAsync_RequestsTheBodyField()
    {
        var emptyResponse = """{ "data": { "search": { "pageInfo": { "hasNextPage": false, "endCursor": null }, "nodes": [] } } }""";
        var handler = new FakeHttpMessageHandler(emptyResponse);
        var client = CreateClient(handler);

        await client.FetchDependabotPrsAsync([new RepositoryInfo("octocat", "repo", IsArchived: false, IsFork: false)]);

        var requestBody = Assert.Single(handler.RequestBodies);
        Assert.Contains("body", requestBody);
    }

    [Fact]
    public async Task FetchDependabotPrsAsync_MapsBody()
    {
        var handler = new FakeHttpMessageHandler(SingleNodeResponse("""
            "reviewDecision": null,
            "mergeStateStatus": "BEHIND",
            "body": "Dependabot is rebasing this PR due to a merge conflict.",
            "commits": { "nodes": [] }
            """));
        var client = CreateClient(handler);

        var prs = await client.FetchDependabotPrsAsync([new RepositoryInfo("octocat", "repo", IsArchived: false, IsFork: false)]);

        Assert.Equal("Dependabot is rebasing this PR due to a merge conflict.", Assert.Single(prs).Body);
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

    [Fact]
    public async Task FetchDependabotPrsAsync_RequestsVulnerabilityAlertsAsASiblingOfSearch()
    {
        var handler = new FakeHttpMessageHandler(SingleNodeResponse("""
            "reviewDecision": null,
            "mergeStateStatus": "CLEAN",
            "commits": { "nodes": [] }
            """));
        var client = CreateClient(handler);

        await client.FetchDependabotPrsAsync([new RepositoryInfo("octocat", "repo", IsArchived: false, IsFork: false)]);

        var requestBody = Assert.Single(handler.RequestBodies);
        Assert.Contains("vulnerabilityAlerts", requestBody);
        Assert.Contains("dependabotUpdate", requestBody);
        Assert.Contains("repository(owner:", requestBody);
    }

    [Fact]
    public async Task FetchDependabotPrsAsync_MarksPrAsSecurityUpdateWhenItAppearsInVulnerabilityAlerts()
    {
        var response = $$"""
            {
              "data": {
                "search": {
                  "pageInfo": { "hasNextPage": false, "endCursor": null },
                  "nodes": [
                    {{PrNodeJson(number: 42, repo: "repo")}}
                  ]
                },
                "repo0": {
                  "name": "repo",
                  "vulnerabilityAlerts": {
                    "nodes": [
                      { "dependabotUpdate": { "pullRequest": { "number": 42 } } }
                    ]
                  }
                }
              }
            }
            """;
        var handler = new FakeHttpMessageHandler(response);
        var client = CreateClient(handler);

        var prs = await client.FetchDependabotPrsAsync([new RepositoryInfo("octocat", "repo", IsArchived: false, IsFork: false)]);

        Assert.True(Assert.Single(prs).IsSecurityUpdate);
    }

    [Fact]
    public async Task FetchDependabotPrsAsync_DoesNotMarkPrAsSecurityUpdateWhenItsNumberIsAbsentFromAlerts()
    {
        var response = $$"""
            {
              "data": {
                "search": {
                  "pageInfo": { "hasNextPage": false, "endCursor": null },
                  "nodes": [
                    {{PrNodeJson(number: 42, repo: "repo")}}
                  ]
                },
                "repo0": {
                  "name": "repo",
                  "vulnerabilityAlerts": { "nodes": [] }
                }
              }
            }
            """;
        var handler = new FakeHttpMessageHandler(response);
        var client = CreateClient(handler);

        var prs = await client.FetchDependabotPrsAsync([new RepositoryInfo("octocat", "repo", IsArchived: false, IsFork: false)]);

        Assert.False(Assert.Single(prs).IsSecurityUpdate);
    }

    [Fact]
    public async Task FetchDependabotPrsAsync_CrossReferencesAlertsPerRepoNotJustByPrNumber()
    {
        var response = $$"""
            {
              "data": {
                "search": {
                  "pageInfo": { "hasNextPage": false, "endCursor": null },
                  "nodes": [
                    {{PrNodeJson(number: 7, repo: "repo-a")}},
                    {{PrNodeJson(number: 7, repo: "repo-b")}}
                  ]
                },
                "repo0": {
                  "name": "repo-a",
                  "vulnerabilityAlerts": {
                    "nodes": [ { "dependabotUpdate": { "pullRequest": { "number": 7 } } } ]
                  }
                },
                "repo1": {
                  "name": "repo-b",
                  "vulnerabilityAlerts": { "nodes": [] }
                }
              }
            }
            """;
        var handler = new FakeHttpMessageHandler(response);
        var client = CreateClient(handler);

        var prs = await client.FetchDependabotPrsAsync([
            new RepositoryInfo("octocat", "repo-a", IsArchived: false, IsFork: false),
            new RepositoryInfo("octocat", "repo-b", IsArchived: false, IsFork: false),
        ]);

        Assert.True(prs.Single(p => p.Repo == "repo-a").IsSecurityUpdate);
        Assert.False(prs.Single(p => p.Repo == "repo-b").IsSecurityUpdate);
    }

    [Fact]
    public async Task FetchDependabotPrsAsync_IgnoresVulnerabilityAlertsNotLinkedToADependabotPullRequest()
    {
        var response = $$"""
            {
              "data": {
                "search": {
                  "pageInfo": { "hasNextPage": false, "endCursor": null },
                  "nodes": [
                    {{PrNodeJson(number: 42, repo: "repo")}}
                  ]
                },
                "repo0": {
                  "name": "repo",
                  "vulnerabilityAlerts": {
                    "nodes": [
                      { "dependabotUpdate": null }
                    ]
                  }
                }
              }
            }
            """;
        var handler = new FakeHttpMessageHandler(response);
        var client = CreateClient(handler);

        var prs = await client.FetchDependabotPrsAsync([new RepositoryInfo("octocat", "repo", IsArchived: false, IsFork: false)]);

        Assert.False(Assert.Single(prs).IsSecurityUpdate);
    }

    [Fact]
    public async Task RefetchAsync_WhenPrIsStillOpen_ReturnsUpdatedPr()
    {
        var handler = new FakeHttpMessageHandler(SingleByNumberResponse("""
            "reviewDecision": "APPROVED",
            "mergeStateStatus": "CLEAN",
            "state": "OPEN",
            "commits": { "nodes": [ { "commit": { "statusCheckRollup": { "state": "SUCCESS" } } } ] }
            """));
        var client = CreateClient(handler);
        var pr = SamplePr();

        var refetched = await client.RefetchAsync("octocat", pr);

        Assert.NotNull(refetched);
        Assert.Equal(42, refetched.Number);
        Assert.Equal("CLEAN", refetched.MergeStateStatus);
        Assert.Equal(CiStatus.Passing, refetched.Ci);
        Assert.Equal(ReviewStatus.Approved, refetched.Review);
    }

    [Fact]
    public async Task RefetchAsync_WhenPrIsNoLongerFound_ReturnsNull()
    {
        var handler = new FakeHttpMessageHandler("""{ "data": { "repository": { "pullRequest": null } } }""");
        var client = CreateClient(handler);

        var refetched = await client.RefetchAsync("octocat", SamplePr());

        Assert.Null(refetched);
    }

    [Fact]
    public async Task RefetchAsync_WhenPrIsClosed_ReturnsNull()
    {
        var handler = new FakeHttpMessageHandler(SingleByNumberResponse("""
            "reviewDecision": "APPROVED",
            "mergeStateStatus": "CLEAN",
            "state": "CLOSED",
            "commits": { "nodes": [] }
            """));
        var client = CreateClient(handler);

        var refetched = await client.RefetchAsync("octocat", SamplePr());

        Assert.Null(refetched);
    }

    [Fact]
    public async Task RefetchAsync_WhenStateChangedToConflicting_ReturnsPrWithUpdatedMergeStateStatus()
    {
        var handler = new FakeHttpMessageHandler(SingleByNumberResponse("""
            "reviewDecision": "APPROVED",
            "mergeStateStatus": "DIRTY",
            "state": "OPEN",
            "commits": { "nodes": [ { "commit": { "statusCheckRollup": { "state": "SUCCESS" } } } ] }
            """));
        var client = CreateClient(handler);

        var refetched = await client.RefetchAsync("octocat", SamplePr());

        Assert.NotNull(refetched);
        Assert.Equal("DIRTY", refetched.MergeStateStatus);
    }

    [Fact]
    public async Task RefetchAsync_MapsBody()
    {
        var handler = new FakeHttpMessageHandler(SingleByNumberResponse("""
            "reviewDecision": null,
            "mergeStateStatus": "BEHIND",
            "state": "OPEN",
            "body": "Dependabot is rebasing this PR due to a merge conflict.",
            "commits": { "nodes": [] }
            """));
        var client = CreateClient(handler);

        var refetched = await client.RefetchAsync("octocat", SamplePr());

        Assert.Equal("Dependabot is rebasing this PR due to a merge conflict.", refetched?.Body);
    }

    [Fact]
    public async Task RefetchAsync_SendsOwnerRepoAndNumberAsVariables()
    {
        var handler = new FakeHttpMessageHandler(SingleByNumberResponse("""
            "reviewDecision": null,
            "mergeStateStatus": "CLEAN",
            "state": "OPEN",
            "commits": { "nodes": [] }
            """));
        var client = CreateClient(handler);

        await client.RefetchAsync("octocat", SamplePr());

        var requestBody = Assert.Single(handler.RequestBodies);
        Assert.Contains("\"owner\":\"octocat\"", requestBody);
        Assert.Contains("\"name\":\"sample-repo\"", requestBody);
        Assert.Contains("\"number\":42", requestBody);
    }

    [Fact]
    public async Task RefetchAsync_CarriesForwardTheKnownIsSecurityUpdateFlag()
    {
        var handler = new FakeHttpMessageHandler(SingleByNumberResponse("""
            "reviewDecision": "APPROVED",
            "mergeStateStatus": "CLEAN",
            "state": "OPEN",
            "commits": { "nodes": [] }
            """));
        var client = CreateClient(handler);
        var pr = SamplePr() with { IsSecurityUpdate = true };

        var refetched = await client.RefetchAsync("octocat", pr);

        Assert.True(refetched?.IsSecurityUpdate);
    }

    [Fact]
    public async Task RefetchAsync_WhenTheApiReturnsErrors_Throws()
    {
        var handler = new FakeHttpMessageHandler("""{ "data": null, "errors": [ { "message": "something went wrong" } ] }""");
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.RefetchAsync("octocat", SamplePr()));
        Assert.Contains("something went wrong", exception.Message);
    }

    private static DependabotPr SamplePr() => new()
    {
        Repo = "sample-repo",
        Number = 42,
        Title = "Bump firebase-tools from 11.2.0 to 11.3.1",
        Url = "https://github.com/octocat/sample-repo/pull/42",
        HeadRefName = "dependabot/npm_and_yarn/firebase-tools-11.3.1",
        MergeStateStatus = "CLEAN",
    };

    private static string SingleByNumberResponse(string extraFields) => $$"""
        {
          "data": {
            "repository": {
              "pullRequest": {
                "number": 42,
                "title": "Bump firebase-tools from 11.2.0 to 11.3.1",
                "url": "https://github.com/octocat/sample-repo/pull/42",
                "headRefName": "dependabot/npm_and_yarn/firebase-tools-11.3.1",
                "isDraft": false,
                "updatedAt": "2026-08-01T12:00:00Z",
                "repository": { "name": "sample-repo" },
                "labels": { "nodes": [] },
                {{extraFields}}
              }
            }
          }
        }
        """;

    private static GraphQlClient CreateClient(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        return new GraphQlClient(httpClient);
    }

    private static string PrNodeJson(int number, string repo, string title = "Bump some-dependency from 1.0.0 to 1.1.0") => $$"""
        {
          "number": {{number}},
          "title": "{{title}}",
          "url": "https://github.com/octocat/{{repo}}/pull/{{number}}",
          "headRefName": "dependabot/npm_and_yarn/some-dependency-1.1.0",
          "isDraft": false,
          "updatedAt": "2026-08-01T12:00:00Z",
          "reviewDecision": null,
          "mergeStateStatus": "CLEAN",
          "repository": { "name": "{{repo}}" },
          "labels": { "nodes": [] },
          "commits": { "nodes": [] }
        }
        """;

    private static string SingleNodeResponse(string extraFields, int number = 1, bool hasNextPage = false, string? endCursor = null, string title = "Bump some-dependency from 1.0.0 to 1.1.0") => $$"""
        {
          "data": {
            "search": {
              "pageInfo": { "hasNextPage": {{(hasNextPage ? "true" : "false")}}, "endCursor": {{(endCursor is null ? "null" : $"\"{endCursor}\"")}} },
              "nodes": [
                {
                  "number": {{number}},
                  "title": "{{title}}",
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
