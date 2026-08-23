namespace Chorectl.Core.GitHub;

/// <summary>
/// Hand-written GraphQL query documents used by <see cref="GraphQlClient"/>.
/// </summary>
public static class Queries
{
    private const string SearchNodeFields = """
        ... on PullRequest {
          number
          title
          url
          headRefName
          isDraft
          updatedAt
          reviewDecision
          mergeStateStatus
          mergeable
          body
          repository {
            name
          }
          commits(last: 1) {
            nodes {
              commit {
                statusCheckRollup {
                  state
                }
              }
            }
          }
        }
        """;

    /// <summary>
    /// Searches for pull requests matching a search-syntax query string, paginated via a cursor.
    /// <paramref name="repositoryAliases"/> is one aliased <c>repository(...)</c> field per repo in
    /// the batch (built by <see cref="GraphQlClient"/>), each pulling <c>vulnerabilityAlerts</c> -
    /// the signal cross-referenced against the search results to set
    /// <see cref="Chorectl.Core.Domain.Dependabot.DependabotPr.IsSecurityUpdate"/>. Riding along as
    /// a sibling field on this same query avoids an extra round trip.
    /// </summary>
    public static string DependabotPrSearch(string repositoryAliases) => $$"""
        query($searchQuery: String!, $after: String) {
          search(query: $searchQuery, type: ISSUE, first: 100, after: $after) {
            pageInfo {
              hasNextPage
              endCursor
            }
            nodes {
              {{SearchNodeFields}}
            }
          }
          {{repositoryAliases}}
        }
        """;

    /// <summary>
    /// Re-fetches a single pull request by number, used to re-verify its state immediately
    /// before acting on it.
    /// </summary>
    public const string DependabotPrByNumber = """
        query($owner: String!, $name: String!, $number: Int!) {
          repository(owner: $owner, name: $name) {
            pullRequest(number: $number) {
              number
              title
              url
              headRefName
              isDraft
              updatedAt
              reviewDecision
              mergeStateStatus
              state
              body
              repository {
                name
              }
              commits(last: 1) {
                nodes {
                  commit {
                    statusCheckRollup {
                      state
                    }
                  }
                }
              }
            }
          }
        }
        """;
}
