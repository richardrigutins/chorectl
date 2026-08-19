using System.ComponentModel;
using Spectre.Console.Cli;

namespace Chorectl.Cli.Commands.Dependabot;

/// <summary>
/// Shared <c>-r|--repo</c> option for dependabot commands that can scope discovery and fetch to
/// a single repo instead of every repo.
/// </summary>
public class RepoScopedSettings : CommandSettings
{
    [CommandOption("-r|--repo <NAME>")]
    [Description("Scope to a single repo by name, skipping discovery and fetch for every other repo.")]
    public string? Repo { get; init; }
}
