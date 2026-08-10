using System.Net.Http.Json;
using System.Text.Json;
using Chorectl.Core.Domain.Dependabot;

namespace Chorectl.Core.GitHub;

/// <summary>
/// Fetches open Dependabot pull requests across repos via a handful of batched GraphQL search
/// queries, rather than one REST call per repo per PR.
/// </summary>
public sealed class GraphQlClient(HttpClient httpClient)
{
    // GitHub search enforces a 256-character limit on the query string.
    private const int MaxSearchQueryLength = 256;
    private const string SearchPrefix = "is:pr is:open author:app/dependabot";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Fetches every open Dependabot PR across the given repos.
    /// </summary>
    public async Task<IReadOnlyList<DependabotPr>> FetchDependabotPrsAsync(
        IReadOnlyList<RepositoryInfo> repos,
        CancellationToken cancellationToken = default)
    {
        var results = new List<DependabotPr>();

        foreach (var searchQuery in BuildSearchQueries(repos))
        {
            string? cursor = null;
            do
            {
                var page = await RunSearchAsync(searchQuery, cursor, cancellationToken);
                results.AddRange(page.Nodes.Select(ToDependabotPr));
                cursor = page.PageInfo.HasNextPage ? page.PageInfo.EndCursor : null;
            } while (cursor is not null);
        }

        return results;
    }

    /// <summary>
    /// Re-fetches a single PR's current state, for re-verification immediately before acting on it.
    /// Returns <see langword="null"/> if the PR is no longer open (closed, merged, or not found).
    /// </summary>
    public async Task<DependabotPr?> RefetchAsync(string owner, DependabotPr pr, CancellationToken cancellationToken = default)
    {
        var request = new { query = Queries.DependabotPrByNumber, variables = new { owner, name = pr.Repo, number = pr.Number } };
        using var response = await httpClient.PostAsJsonAsync("graphql", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<PrByNumberResponse>(JsonOptions, cancellationToken);
        if (body?.Errors is { Count: > 0 } errors)
        {
            throw new InvalidOperationException($"GraphQL query failed: {string.Join("; ", errors.Select(e => e.Message))}");
        }

        var node = body?.Data?.Repository?.PullRequest;
        if (node is null || node.State is not (null or "OPEN"))
        {
            return null;
        }

        return ToDependabotPr(node);
    }

    private async Task<SearchConnection> RunSearchAsync(string searchQuery, string? cursor, CancellationToken cancellationToken)
    {
        var request = new { query = Queries.DependabotPrSearch, variables = new { searchQuery, after = cursor } };
        using var response = await httpClient.PostAsJsonAsync("graphql", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<GraphQlResponse>(JsonOptions, cancellationToken);
        if (body?.Errors is { Count: > 0 } errors)
        {
            throw new InvalidOperationException($"GraphQL query failed: {string.Join("; ", errors.Select(e => e.Message))}");
        }

        return body!.Data!.Search;
    }

    private static List<string> BuildSearchQueries(IReadOnlyList<RepositoryInfo> repos)
    {
        var queries = new List<string>();
        var current = SearchPrefix;

        foreach (var repo in repos)
        {
            var repoFilter = $" repo:{repo.Owner}/{repo.Name}";
            if (current.Length + repoFilter.Length > MaxSearchQueryLength)
            {
                queries.Add(current);
                current = SearchPrefix;
            }

            current += repoFilter;
        }

        if (current != SearchPrefix)
        {
            queries.Add(current);
        }

        return queries;
    }

    private static DependabotPr ToDependabotPr(PrNode node) => new()
    {
        Repo = node.Repository.Name,
        Number = node.Number,
        Title = node.Title,
        Url = node.Url,
        HeadRefName = node.HeadRefName,
        IsDraft = node.IsDraft,
        UpdatedAt = node.UpdatedAt,
        Ci = ToCiStatus(node.Commits.Nodes.FirstOrDefault()?.Commit.StatusCheckRollup?.State),
        Review = ToReviewStatus(node.ReviewDecision),
        MergeStateStatus = node.MergeStateStatus,
        SemverLevel = SemverParser.Classify(node.Title),
        DependencyName = SemverParser.ParseDependencyName(node.Title),
        FromVersion = SemverParser.ParseFromVersion(node.Title),
        ToVersion = SemverParser.ParseToVersion(node.Title),
        IsGrouped = SemverParser.IsGrouped(node.Title),
    };

    private static CiStatus ToCiStatus(string? state) => state switch
    {
        "SUCCESS" => CiStatus.Passing,
        "FAILURE" or "ERROR" => CiStatus.Failing,
        "PENDING" or "EXPECTED" => CiStatus.Pending,
        _ => CiStatus.NoChecks,
    };

    private static ReviewStatus ToReviewStatus(string? reviewDecision) => reviewDecision switch
    {
        "APPROVED" => ReviewStatus.Approved,
        "REVIEW_REQUIRED" or "CHANGES_REQUESTED" => ReviewStatus.ReviewRequired,
        _ => ReviewStatus.NotRequired,
    };

    private sealed record GraphQlResponse(GraphQlData? Data, IReadOnlyList<GraphQlError>? Errors);

    private sealed record GraphQlData(SearchConnection Search);

    private sealed record GraphQlError(string Message);

    private sealed record SearchConnection(PageInfo PageInfo, IReadOnlyList<PrNode> Nodes);

    private sealed record PageInfo(bool HasNextPage, string? EndCursor);

    private sealed record PrByNumberResponse(PrByNumberData? Data, IReadOnlyList<GraphQlError>? Errors);

    private sealed record PrByNumberData(RepositoryWithPr? Repository);

    private sealed record RepositoryWithPr(PrNode? PullRequest);

    private sealed record PrNode(
        int Number,
        string Title,
        string Url,
        string HeadRefName,
        bool IsDraft,
        DateTimeOffset UpdatedAt,
        string? ReviewDecision,
        string MergeStateStatus,
        RepositoryRef Repository,
        CommitsConnection Commits,
        string? State = null);

    private sealed record RepositoryRef(string Name);

    private sealed record CommitsConnection(IReadOnlyList<CommitNode> Nodes);

    private sealed record CommitNode(CommitRef Commit);

    private sealed record CommitRef(StatusCheckRollup? StatusCheckRollup);

    private sealed record StatusCheckRollup(string? State);
}
