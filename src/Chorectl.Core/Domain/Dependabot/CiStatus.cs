namespace Chorectl.Core.Domain.Dependabot;

/// <summary>
/// Aggregate CI status for a pull request, derived from its <c>statusCheckRollup</c>.
/// </summary>
public enum CiStatus
{
    Passing,
    Failing,
    Pending,
    NoChecks,
}
