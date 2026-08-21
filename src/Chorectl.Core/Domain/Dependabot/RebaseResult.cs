namespace Chorectl.Core.Domain.Dependabot;

/// <summary>
/// Result of requesting a rebase for a single Dependabot PR as part of a batch.
/// </summary>
public sealed record RebaseResult(DependabotPr Pr, RebaseOutcome Outcome, string? Reason = null);
