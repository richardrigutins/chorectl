using Chorectl.Core.Domain.Dependabot;

namespace Chorectl.Cli.Rendering.Dependabot;

/// <summary>
/// Shared color coding for semver bump levels, so the overview table and selection screens agree
/// on what a glance at "major" vs "patch" should look like.
/// </summary>
internal static class BumpStyle
{
    public static string Markup(SemverLevel level) => level switch
    {
        SemverLevel.Major => "[red]major[/]",
        SemverLevel.Minor => "[yellow]minor[/]",
        SemverLevel.Patch => "[green]patch[/]",
        _ => "[grey]unknown[/]",
    };
}
