using System.ComponentModel;
using Spectre.Console.Cli;

namespace Chorectl.Cli.Commands.Dependabot;

/// <summary>
/// Flags shared by every <c>dependabot</c> subcommand: <c>-r|--repo</c> to scope discovery and
/// fetch to a single repo, <c>-s|--security</c> to filter to security-update PRs, <c>--json</c>
/// to print structured JSON instead of rendering the TUI, and <c>--verbose</c> to print
/// diagnostic detail about discovery/fetch/state-check steps.
/// </summary>
public class DependabotSettings : CommandSettings
{
    [CommandOption("-r|--repo <NAME>")]
    [Description("Scope to a single repo by name, skipping discovery and fetch for every other repo.")]
    public string? Repo { get; init; }

    [CommandOption("-s|--security")]
    [Description("Filter to PRs where IsSecurityUpdate is true, before any other candidate-set filtering.")]
    public bool Security { get; init; }

    [CommandOption("--json")]
    [Description("Disable TUI rendering and print structured JSON output.")]
    public bool Json { get; init; }

    [CommandOption("--verbose")]
    [Description("Print diagnostic detail about discovery, fetch, and state-check steps.")]
    public bool Verbose { get; init; }
}
