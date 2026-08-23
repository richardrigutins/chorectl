using Chorectl.Cli.Rendering;
using Chorectl.Cli.Rendering.Dependabot;
using Chorectl.Core.GitHub;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Chorectl.Cli.Commands.Dependabot;

/// <summary>
/// <c>chorectl dependabot list</c> - prints a status table of every open Dependabot PR across
/// the user's non-archived, non-fork repos.
/// </summary>
public sealed class ListCommand(RestClient restClient, GraphQlClient graphQlClient, IAnsiConsole console) : AsyncCommand<ListCommand.Settings>
{
    public sealed class Settings : DependabotSettings;

    protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken) =>
        RunAsync(settings.Repo, settings.Security, settings.Json, settings.Verbose);

    /// <summary>Discovers repos, fetches open Dependabot PRs, and renders the overview table (or JSON).</summary>
    public async Task<int> RunAsync(string? repo = null, bool security = false, bool json = false, bool verbose = false)
    {
        var repos = await restClient.DiscoverReposAsync(repo);
        VerboseLog.Write(console, verbose, json, $"Discovered {repos.Count} repo(s)");

        var prs = await graphQlClient.FetchDependabotPrsAsync(repos);
        VerboseLog.Write(console, verbose, json, $"Fetched {prs.Count} Dependabot PR(s)");

        if (security)
        {
            prs = prs.Where(pr => pr.IsSecurityUpdate).ToList();
        }

        if (json)
        {
            JsonOutput.Write(console, prs);
        }
        else
        {
            OverviewTable.Render(console, prs);
        }

        return 0;
    }
}
