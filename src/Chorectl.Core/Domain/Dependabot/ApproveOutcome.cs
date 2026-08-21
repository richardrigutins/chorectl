namespace Chorectl.Core.Domain.Dependabot;

/// <summary>
/// Outcome of approving a single Dependabot PR.
/// </summary>
public enum ApproveOutcome
{
    Approved,
    Failed,
}
