namespace Chorectl.Core.Audit;

/// <summary>
/// One recorded action against a Dependabot PR (merged, rebase-requested, approved, skipped, failed).
/// </summary>
public sealed record AuditEntry(DateTimeOffset Timestamp, string Repo, int PrNumber, string Action, string? Reason = null);
