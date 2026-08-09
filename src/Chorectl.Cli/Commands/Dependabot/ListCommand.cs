using Chorectl.Cli.Rendering.Dependabot;
using Chorectl.Core.GitHub;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Chorectl.Cli.Commands.Dependabot;

/// <summary>
/// <c>chorectl dependabot list</c> — prints a status table of every open Dependabot PR across
/// the user's non-archived, non-fork repos.
/// </summary>
public sealed class ListCommand(RestClient restClient, GraphQlClient graphQlClient, IAnsiConsole console) : AsyncCommand
{
    protected override Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken) => RunAsync();

    /// <summary>Discovers repos, fetches open Dependabot PRs, and renders the overview table.</summary>
    public async Task<int> RunAsync()
    {
        var repos = await restClient.DiscoverReposAsync();
        var prs = await graphQlClient.FetchDependabotPrsAsync(repos);

        OverviewTable.Render(console, prs);

        return 0;
    }
}
