using System.Text.RegularExpressions;

namespace Chorectl.Core.Domain.Dependabot;

/// <summary>
/// Parses the "Bump &lt;dependency&gt; from &lt;old&gt; to &lt;new&gt;" PR title Dependabot always
/// generates and diffs the two versions into a semver bump level.
/// </summary>
public static partial class SemverParser
{
    [GeneratedRegex(@"^Bump .+ from (?<from>\S+) to (?<to>\S+)(?: in .+)?$")]
    private static partial Regex TitlePattern();

    // Leniently coerces version-tag conventions like "v3" (GitHub Actions) to 3.0.0. A bare
    // integer with no "v" prefix and no dot (e.g. a calver-style "20230101") is rejected rather
    // than coerced, since that's the date-based-version case Dependabot titles can also contain.
    [GeneratedRegex(@"^(?<v>[vV])?(?<major>\d+)(?:\.(?<minor>\d+))?(?:\.(?<patch>\d+))?")]
    private static partial Regex VersionPattern();

    /// <summary>
    /// Classifies a Dependabot PR title's version bump as patch, minor, or major. Returns
    /// <see cref="SemverLevel.Unknown"/> if the title doesn't match Dependabot's format or either
    /// version isn't parseable, even leniently.
    /// </summary>
    public static SemverLevel Classify(string title)
    {
        var titleMatch = TitlePattern().Match(title);
        if (!titleMatch.Success)
        {
            return SemverLevel.Unknown;
        }

        if (!TryParseVersion(titleMatch.Groups["from"].Value, out var from) ||
            !TryParseVersion(titleMatch.Groups["to"].Value, out var to))
        {
            return SemverLevel.Unknown;
        }

        if (from.Major != to.Major)
        {
            return SemverLevel.Major;
        }

        return from.Minor != to.Minor ? SemverLevel.Minor : SemverLevel.Patch;
    }

    private static bool TryParseVersion(string version, out (int Major, int Minor, int Patch) parsed)
    {
        var match = VersionPattern().Match(version);
        var hasVPrefix = match.Groups["v"].Success;
        var hasMinor = match.Groups["minor"].Success;

        if (!match.Success || (!hasVPrefix && !hasMinor))
        {
            parsed = default;
            return false;
        }

        parsed = (
            int.Parse(match.Groups["major"].Value),
            hasMinor ? int.Parse(match.Groups["minor"].Value) : 0,
            match.Groups["patch"].Success ? int.Parse(match.Groups["patch"].Value) : 0);
        return true;
    }
}
