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

        foreach (var batch in BuildSearchBatches(repos))
        {
            var query = Queries.DependabotPrSearch(BuildRepositoryAliases(batch.Repos));
            string? cursor = null;
            do
            {
                var (search, securityPrs) = await RunSearchAsync(query, batch.SearchQuery, cursor, cancellationToken);
                results.AddRange(search.Nodes.Select(node =>
                    ToDependabotPr(node, securityPrs.Contains((node.Repository.Name, node.Number)))));
                cursor = search.PageInfo.HasNextPage ? search.PageInfo.EndCursor : null;
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

        // The by-number query has no vulnerabilityAlerts data of its own; security status doesn't
        // change during a merge poll window, so the last known value carries forward.
        return ToDependabotPr(node, pr.IsSecurityUpdate);
    }

    private async Task<(SearchConnection Search, HashSet<(string Repo, int Number)> SecurityPrs)> RunSearchAsync(
        string query, string searchQuery, string? cursor, CancellationToken cancellationToken)
    {
        var request = new { query, variables = new { searchQuery, after = cursor } };
        using var response = await httpClient.PostAsJsonAsync("graphql", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("errors", out var errorsEl) && errorsEl.ValueKind == JsonValueKind.Array && errorsEl.GetArrayLength() > 0)
        {
            var messages = errorsEl.EnumerateArray().Select(e => e.GetProperty("message").GetString());
            throw new InvalidOperationException($"GraphQL query failed: {string.Join("; ", messages)}");
        }

        var data = root.GetProperty("data");
        var search = data.GetProperty("search").Deserialize<SearchConnection>(JsonOptions)!;
        return (search, ParseSecurityPrs(data));
    }

    /// <summary>
    /// Cross-references each aliased <c>repository(...)</c> field's <c>vulnerabilityAlerts</c>
    /// against <c>dependabotUpdate.pullRequest.number</c> to build the set of (repo, PR number)
    /// pairs that are security updates. See <see cref="Queries.DependabotPrSearch"/>.
    /// </summary>
    private static HashSet<(string Repo, int Number)> ParseSecurityPrs(JsonElement data)
    {
        var securityPrs = new HashSet<(string, int)>();
        foreach (var property in data.EnumerateObject())
        {
            if (property.Name == "search" || property.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!property.Value.TryGetProperty("name", out var nameEl)
                || !property.Value.TryGetProperty("vulnerabilityAlerts", out var alertsEl)
                || alertsEl.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var repoName = nameEl.GetString()!;
            foreach (var alert in alertsEl.GetProperty("nodes").EnumerateArray())
            {
                if (alert.TryGetProperty("dependabotUpdate", out var duEl) && duEl.ValueKind == JsonValueKind.Object
                    && duEl.TryGetProperty("pullRequest", out var prEl) && prEl.ValueKind == JsonValueKind.Object)
                {
                    securityPrs.Add((repoName, prEl.GetProperty("number").GetInt32()));
                }
            }
        }

        return securityPrs;
    }

    private static List<SearchBatch> BuildSearchBatches(IReadOnlyList<RepositoryInfo> repos)
    {
        var batches = new List<SearchBatch>();
        var currentQuery = SearchPrefix;
        var currentRepos = new List<RepositoryInfo>();

        foreach (var repo in repos)
        {
            var repoFilter = $" repo:{repo.Owner}/{repo.Name}";
            if (currentQuery.Length + repoFilter.Length > MaxSearchQueryLength)
            {
                batches.Add(new SearchBatch(currentQuery, currentRepos));
                currentQuery = SearchPrefix;
                currentRepos = [];
            }

            currentQuery += repoFilter;
            currentRepos.Add(repo);
        }

        if (currentQuery != SearchPrefix)
        {
            batches.Add(new SearchBatch(currentQuery, currentRepos));
        }

        return batches;
    }

    private static string BuildRepositoryAliases(IReadOnlyList<RepositoryInfo> repos) =>
        string.Join(Environment.NewLine, repos.Select((repo, i) => $$"""
            repo{{i}}: repository(owner: {{JsonSerializer.Serialize(repo.Owner)}}, name: {{JsonSerializer.Serialize(repo.Name)}}) {
              name
              vulnerabilityAlerts(first: 100) {
                nodes {
                  dependabotUpdate {
                    pullRequest {
                      number
                    }
                  }
                }
              }
            }
            """));

    private static DependabotPr ToDependabotPr(PrNode node, bool isSecurityUpdate = false) => new()
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
        Body = node.Body,
        SemverLevel = SemverParser.Classify(node.Title),
        DependencyName = SemverParser.ParseDependencyName(node.Title),
        FromVersion = SemverParser.ParseFromVersion(node.Title),
        ToVersion = SemverParser.ParseToVersion(node.Title),
        IsGrouped = SemverParser.IsGrouped(node.Title),
        IsSecurityUpdate = isSecurityUpdate,
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

    private sealed record SearchBatch(string SearchQuery, IReadOnlyList<RepositoryInfo> Repos);

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
        string? State = null,
        string? Body = null);

    private sealed record RepositoryRef(string Name);

    private sealed record CommitsConnection(IReadOnlyList<CommitNode> Nodes);

    private sealed record CommitNode(CommitRef Commit);

    private sealed record CommitRef(StatusCheckRollup? StatusCheckRollup);

    private sealed record StatusCheckRollup(string? State);
}
