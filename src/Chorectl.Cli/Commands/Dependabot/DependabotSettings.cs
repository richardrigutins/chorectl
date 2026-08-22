using System.ComponentModel;
using Spectre.Console.Cli;

namespace Chorectl.Cli.Commands.Dependabot;

/// <summary>
/// Flags shared by every <c>dependabot</c> subcommand: <c>-r|--repo</c> to scope discovery and
/// fetch to a single repo, and <c>--json</c> to print structured JSON instead of rendering the TUI.
/// </summary>
public class DependabotSettings : CommandSettings
{
    [CommandOption("-r|--repo <NAME>")]
    [Description("Scope to a single repo by name, skipping discovery and fetch for every other repo.")]
    public string? Repo { get; init; }

    [CommandOption("--json")]
    [Description("Disable TUI rendering and print structured JSON output.")]
    public bool Json { get; init; }
}
