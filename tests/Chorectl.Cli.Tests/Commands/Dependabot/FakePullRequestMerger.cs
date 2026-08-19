using Chorectl.Core.Domain.Dependabot;
using Chorectl.Core.GitHub;

namespace Chorectl.Cli.Tests.Commands.Dependabot;

internal sealed class FakePullRequestMerger : IPullRequestMerger
{
    private readonly HashSet<int> alreadyFailedOnce = [];

    public List<(string Owner, DependabotPr Pr)> MergeCalls { get; } = [];

    /// <summary>PR numbers that fail on every merge attempt.</summary>
    public HashSet<int> FailForPrNumbers { get; } = [];

    /// <summary>PR numbers that fail only their first merge attempt, then succeed.</summary>
    public HashSet<int> FailOnceForPrNumbers { get; } = [];

    public string? FailureMessage { get; set; }

    public Task MergeAsync(string owner, DependabotPr pr, CancellationToken cancellationToken = default)
    {
        MergeCalls.Add((owner, pr));

        var failsThisCall = FailForPrNumbers.Contains(pr.Number)
            || (FailOnceForPrNumbers.Contains(pr.Number) && alreadyFailedOnce.Add(pr.Number));

        if (failsThisCall)
        {
            throw new InvalidOperationException(FailureMessage ?? $"merge conflict on #{pr.Number}");
        }

        return Task.CompletedTask;
    }
}
