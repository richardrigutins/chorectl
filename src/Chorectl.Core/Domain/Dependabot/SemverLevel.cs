namespace Chorectl.Core.Domain.Dependabot;

/// <summary>
/// Semver bump level of a Dependabot update, parsed from the PR title.
/// </summary>
public enum SemverLevel
{
    Patch,
    Minor,
    Major,
    Unknown,
}
