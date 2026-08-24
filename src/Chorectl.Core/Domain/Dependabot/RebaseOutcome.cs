namespace Chorectl.Core.Domain.Dependabot;

/// <summary>
/// Outcome of requesting a rebase for a single Dependabot PR.
/// </summary>
public enum RebaseOutcome
{
    Requested,
    Skipped,
    Failed,
}
