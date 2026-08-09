using System.Text.RegularExpressions;

namespace Chorectl.Core.Domain.Dependabot;

/// <summary>
/// Parses the "Bump &lt;dependency&gt; from &lt;old&gt; to &lt;new&gt;" PR title Dependabot always
/// generates and diffs the two versions into a semver bump level.
/// </summary>
public static partial class SemverParser
{
    // Dependabot always generates "Bump <dependency> from <old> to <new>", but a repo's
    // dependabot.yml commit-message.prefix setting can prepend a conventional-commit-style
    // prefix (e.g. "chore: bump ..." or "[Chore] Bump ...") and lowercase "bump", so the match
    // isn't anchored to the start of the title and is case-insensitive on "bump".
    [GeneratedRegex(@"bump (?<dependency>.+) from (?<from>\S+) to (?<to>\S+)(?: in .+)?$", RegexOptions.IgnoreCase)]
    private static partial Regex TitlePattern();

    // Leniently coerces version-tag conventions like "v3" (GitHub Actions) to 3.0.0, and bare
    // major-only tags like "3" (also common for GitHub Actions, e.g. "gittools/actions from 3 to
    // 4") the same way.
    [GeneratedRegex(@"^(?<v>[vV])?(?<major>\d+)(?:\.(?<minor>\d+))?(?:\.(?<patch>\d+))?")]
    private static partial Regex VersionPattern();

    // ponytail: a bare integer is only coerced to a major-only version when it's short enough to
    // plausibly be one; a calver-style date (e.g. "20230101") is always longer than this and
    // stays rejected. Revisit with a real calver detector if this heuristic misfires in practice.
    private const int MaxBareMajorVersionDigits = 3;

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

    /// <summary>
    /// Extracts the dependency name from a Dependabot PR title. Returns <c>null</c> if the title
    /// doesn't match Dependabot's format.
    /// </summary>
    public static string? ParseDependencyName(string title)
    {
        var match = TitlePattern().Match(title);
        return match.Success ? match.Groups["dependency"].Value : null;
    }

    private static bool TryParseVersion(string version, out (int Major, int Minor, int Patch) parsed)
    {
        var match = VersionPattern().Match(version);
        var hasVPrefix = match.Groups["v"].Success;
        var hasMinor = match.Groups["minor"].Success;
        var majorGroup = match.Groups["major"];
        var isPlausibleBareMajor = majorGroup.Success && majorGroup.Value.Length <= MaxBareMajorVersionDigits;

        if (!match.Success || (!hasVPrefix && !hasMinor && !isPlausibleBareMajor))
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
