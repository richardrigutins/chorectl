namespace Chorectl.Core.Domain.Dependabot;

/// <summary>
/// Result of acting on a single Dependabot PR as part of a batch (merge/rebase/approve). Generic
/// over <typeparamref name="TOutcome"/> so each command keeps its own outcome enum - and therefore
/// its own verb in <c>--json</c> output (<c>merged</c>/<c>requested</c>/<c>approved</c>) - while
/// sharing one result shape.
/// </summary>
public sealed record BatchResult<TOutcome>(DependabotPr Pr, TOutcome Outcome, string? Reason = null)
    where TOutcome : struct, Enum;
