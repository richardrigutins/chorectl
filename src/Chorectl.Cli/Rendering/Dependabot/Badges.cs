using Chorectl.Core.Domain.Dependabot;

namespace Chorectl.Cli.Rendering.Dependabot;

/// <summary>
/// Shared visual badges for PR properties that don't fit a dedicated table column - grouped and
/// security updates - so the overview table and selection screens flag them consistently. Grouped
/// PRs are excluded from default selection regardless of semver level (see
/// <see cref="Classifier.DefaultSelected"/>); the security badge is purely informational - a
/// security update is otherwise selected the same as any other PR at its semver level.
/// </summary>
internal static class Badges
{
    public static string Markup(DependabotPr pr)
    {
        var badges = new List<string>();
        if (pr.IsSecurityUpdate)
        {
            badges.Add("[red]security[/]");
        }

        if (pr.IsGrouped)
        {
            badges.Add("[grey]grouped[/]");
        }

        return badges.Count == 0 ? string.Empty : $"  [[{string.Join(", ", badges)}]]";
    }
}
