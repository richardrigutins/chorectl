using Chorectl.Core.Config;

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
        && pr.MergeStateStatus != MergeStateStatuses.Dirty
        && !pr.IsDraft;

    /// <summary>Whether a PR needs a rebase before it can be merged.</summary>
    public static bool NeedsRebase(DependabotPr pr) =>
        pr.MergeStateStatus is MergeStateStatuses.Dirty or MergeStateStatuses.Behind;

    /// <summary>Whether a PR is blocked on a required review.</summary>
    public static bool NeedsApproval(DependabotPr pr) =>
        pr.Review == ReviewStatus.ReviewRequired;

    /// <summary>
    /// Whether a PR should be pre-selected on the merge screen, per <paramref name="defaultSelect"/>
    /// (the user's <c>default_select.*</c> config). An <see cref="SemverLevel.Unknown"/> bump is
    /// never pre-selected regardless of config - an unparseable version isn't a known risk level
    /// the user can opt into, unlike major. Security updates are excluded regardless of semver
    /// level or config too - they deserve a manual look even at patch level (there's no
    /// <c>default_select.security</c> toggle by design).
    /// </summary>
    public static bool DefaultSelected(DependabotPr pr, DefaultSelectConfig defaultSelect) =>
        IsReadyToMerge(pr) && MatchesRiskTier(pr, defaultSelect);

    /// <summary>
    /// Whether a PR should be pre-selected on the approve screen, per <paramref name="defaultSelect"/>.
    /// Applies the same risk-tier rules as <see cref="DefaultSelected"/> (semver level, grouped,
    /// security) but without the <see cref="IsReadyToMerge"/> gate - every PR here already needs
    /// approval (<see cref="ReviewStatus.ReviewRequired"/>), which <see cref="IsReadyToMerge"/>
    /// would otherwise disqualify outright.
    /// </summary>
    public static bool DefaultSelectedForApproval(DependabotPr pr, DefaultSelectConfig defaultSelect) =>
        MatchesRiskTier(pr, defaultSelect);

    private static bool MatchesRiskTier(DependabotPr pr, DefaultSelectConfig defaultSelect) =>
        IsSemverLevelAllowed(pr.SemverLevel, defaultSelect)
        && (defaultSelect.Grouped || !pr.IsGrouped)
        && !pr.IsSecurityUpdate;

    private static bool IsSemverLevelAllowed(SemverLevel level, DefaultSelectConfig defaultSelect) => level switch
    {
        SemverLevel.Patch => defaultSelect.Patch,
        SemverLevel.Minor => defaultSelect.Minor,
        SemverLevel.Major => defaultSelect.Major,
        _ => false,
    };

    /// <summary>
    /// Whether the PR body currently carries Dependabot's temporary rebase-in-progress banner.
    /// Loose substring match, not an exact string, since Dependabot could reword it - display-only,
    /// doesn't affect poll/timeout behavior.
    /// </summary>
    public static bool HasRebaseBanner(DependabotPr pr) =>
        pr.Body is not null && pr.Body.Contains("rebasing this", StringComparison.OrdinalIgnoreCase);
}
