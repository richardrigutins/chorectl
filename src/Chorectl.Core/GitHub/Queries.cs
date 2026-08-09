namespace Chorectl.Core.GitHub;

/// <summary>
/// Hand-written GraphQL query documents used by <see cref="GraphQlClient"/>.
/// </summary>
public static class Queries
{
    /// <summary>
    /// Searches for pull requests matching a search-syntax query string, paginated via a cursor.
    /// </summary>
    public const string DependabotPrSearch = """
        query($searchQuery: String!, $after: String) {
          search(query: $searchQuery, type: ISSUE, first: 100, after: $after) {
            pageInfo {
              hasNextPage
              endCursor
            }
            nodes {
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
                repository {
                  name
                }
                labels(first: 20) {
                  nodes {
                    name
                  }
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
        }
        """;
}
