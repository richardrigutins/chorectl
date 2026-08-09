namespace Chorectl.Core.Domain.Dependabot;

/// <summary>
/// Outcome of attempting to merge a single Dependabot PR.
/// </summary>
public enum MergeOutcome
{
    Merged,
    Skipped,
    Failed,
}
