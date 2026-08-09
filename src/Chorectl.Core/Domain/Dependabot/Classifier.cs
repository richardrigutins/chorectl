namespace Chorectl.Core.Domain.Dependabot;

/// <summary>
/// Pure classification predicates shared by the list, merge, rebase, and approve commands.
/// </summary>
public static class Classifier
{
    /// <summary>Whether a PR can be merged right now with no further action needed.</summary>
    public static bool IsReadyToMerge(DependabotPr pr) =>
        pr.Ci == CiStatus.Passing
        && pr.Review != ReviewStatus.ReviewRequired
        && pr.MergeStateStatus == "CLEAN"
        && !pr.IsDraft;

    /// <summary>Whether a PR needs a rebase before it can be merged.</summary>
    public static bool NeedsRebase(DependabotPr pr) =>
        pr.MergeStateStatus is "DIRTY" or "BEHIND";

    /// <summary>Whether a PR is blocked on a required review.</summary>
    public static bool NeedsApproval(DependabotPr pr) =>
        pr.Review == ReviewStatus.ReviewRequired;

    /// <summary>Whether a PR should be pre-selected on the merge screen.</summary>
    public static bool DefaultSelected(DependabotPr pr) =>
        IsReadyToMerge(pr)
        && pr.SemverLevel is SemverLevel.Patch or SemverLevel.Minor
        && !pr.IsGrouped;
}
