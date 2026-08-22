using System.ComponentModel;
using Spectre.Console.Cli;

namespace Chorectl.Cli.Commands.Dependabot;

/// <summary>
/// Additional flags for dependabot action commands (merge/rebase/approve), on top of the
/// <c>-r|--repo</c>/<c>--json</c> shared by every dependabot subcommand.
/// </summary>
public class ActionSettings : DependabotSettings
{
    [CommandOption("--dry-run")]
    [Description("Preview the selection and summary without performing any mutations.")]
    public bool DryRun { get; init; }

    [CommandOption("--yes")]
    [Description("Skip the selection screen and act on the default-selected set.")]
    public bool Yes { get; init; }
}
