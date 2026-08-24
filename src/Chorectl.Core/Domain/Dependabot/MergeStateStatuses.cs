namespace Chorectl.Core.Domain.Dependabot;

/// <summary>
/// The known values of <see cref="DependabotPr.MergeStateStatus"/>, as raw strings from the
/// GitHub API (see Section 2 of the project plan: <c>CLEAN</c>, <c>DIRTY</c>, <c>BLOCKED</c>,
/// <c>BEHIND</c>, <c>UNSTABLE</c>, <c>UNKNOWN</c>). Kept as string constants rather than an enum
/// on purpose: <see cref="DependabotPr.MergeStateStatus"/> deliberately stays a raw passthrough
/// string so an unrecognized future value from GitHub degrades gracefully (falls through to the
/// "not DIRTY/BEHIND" and "unrecognized state" branches already in place) instead of failing to
/// deserialize. These constants exist only to give the well-known values compile-time-checked
/// names instead of scattering literal strings across <see cref="Classifier"/> and its callers.
/// </summary>
public static class MergeStateStatuses
{
    public const string Clean = "CLEAN";
    public const string Dirty = "DIRTY";
    public const string Blocked = "BLOCKED";
    public const string Behind = "BEHIND";
    public const string Unstable = "UNSTABLE";
    public const string Unknown = "UNKNOWN";
}
