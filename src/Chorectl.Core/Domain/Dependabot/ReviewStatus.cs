namespace Chorectl.Core.Domain.Dependabot;

/// <summary>
/// Review status for a pull request, derived from its <c>reviewDecision</c>.
/// </summary>
public enum ReviewStatus
{
    Approved,
    ReviewRequired,
    NotRequired,
}
