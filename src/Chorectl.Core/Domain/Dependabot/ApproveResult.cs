namespace Chorectl.Core.Domain.Dependabot;

/// <summary>
/// Result of approving a single Dependabot PR as part of a batch.
/// </summary>
public sealed record ApproveResult(DependabotPr Pr, ApproveOutcome Outcome, string? Reason = null);
