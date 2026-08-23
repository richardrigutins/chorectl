namespace Chorectl.Core.Domain.Dependabot;

/// <summary>
/// Pure classification predicates shared by the list, merge, rebase, and approve commands.
/// </summary>
public static class Classifier
{
    /// <summary>
    /// Whether a PR can be attempted right now. <c>BEHIND</c> doesn't disqualify it - the base
    /// branch has moved but the PR may merge cleanly regardless, so the merge attempt itself is
    /// the real test, not a state flag predicting it in advance. Only <c>DIRTY</c> (an actual
    /// conflict) does.
    /// </summary>
    public static bool IsReadyToMerge(DependabotPr pr) =>
        pr.Ci == CiStatus.Passing
        && pr.Review != ReviewStatus.ReviewRequired
        && pr.MergeStateStatus != "DIRTY"
        && !pr.IsDraft;

    /// <summary>Whether a PR needs a rebase before it can be merged.</summary>
    public static bool NeedsRebase(DependabotPr pr) =>
        pr.MergeStateStatus is "DIRTY" or "BEHIND";

    /// <summary>Whether a PR is blocked on a required review.</summary>
    public static bool NeedsApproval(DependabotPr pr) =>
        pr.Review == ReviewStatus.ReviewRequired;

    /// <summary>
    /// Whether a PR should be pre-selected on the merge screen. Grouped updates are excluded
    /// regardless of semver level - a group can bundle a major bump under a patch-looking title.
    /// Security updates are excluded regardless of semver level too - they deserve a manual look
    /// even at patch level.
    /// </summary>
    public static bool DefaultSelected(DependabotPr pr) =>
        IsReadyToMerge(pr)
        && pr.SemverLevel is SemverLevel.Patch or SemverLevel.Minor
        && !pr.IsGrouped
        && !pr.IsSecurityUpdate;

    /// <summary>
    /// Whether the PR body currently carries Dependabot's temporary rebase-in-progress banner.
    /// Loose substring match, not an exact string, since Dependabot could reword it - display-only,
    /// doesn't affect poll/timeout behavior.
    /// </summary>
    public static bool HasRebaseBanner(DependabotPr pr) =>
        pr.Body is not null && pr.Body.Contains("rebasing this", StringComparison.OrdinalIgnoreCase);
}
