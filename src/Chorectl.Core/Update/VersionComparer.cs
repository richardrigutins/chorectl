namespace Chorectl.Core.Update;

/// <summary>
/// Compares release version strings (tags like <c>v1.2.3</c>, or a bare <c>1.2.3</c>) using
/// <see cref="Version"/> - no hand-rolled semver parsing needed since chorectl's own releases are
/// always plain major.minor.patch, unlike the arbitrary dependency versions <c>SemverParser</c>
/// has to tolerate.
/// </summary>
public static class VersionComparer
{
    public static bool IsNewer(string candidateVersion, string currentVersion) =>
        TryParse(candidateVersion, out var candidate) && TryParse(currentVersion, out var current) && candidate > current;

    // Semver build metadata (a trailing "+..." - e.g. the git commit SHA .NET's SDK appends to
    // InformationalVersion by default, see Directory.Build.props) and prerelease identifiers (a
    // trailing "-..." - e.g. the "0.0.0-dev" placeholder a local build embeds when no version was
    // passed at publish time, see CurrentVersion) never affect precedence here, so both are
    // stripped before parsing rather than treated as part of the version itself.
    private static bool TryParse(string version, out Version parsed) =>
        Version.TryParse(version.Split('+', 2)[0].Split('-', 2)[0].TrimStart('v', 'V'), out parsed!);
}
