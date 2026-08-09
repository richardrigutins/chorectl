namespace Chorectl.Core.Domain.Dependabot;

/// <summary>
/// Result of attempting to merge a single Dependabot PR as part of a batch.
/// </summary>
public sealed record MergeResult(DependabotPr Pr, MergeOutcome Outcome, string? Reason = null);
