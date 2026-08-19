using Chorectl.Core.Domain.Dependabot;
using Chorectl.Core.GitHub;

namespace Chorectl.Cli.Tests.Commands.Dependabot;

internal sealed class FakePullRequestMerger : IPullRequestMerger
{
    private readonly HashSet<int> alreadyFailedOnce = [];

    public List<(string Owner, DependabotPr Pr)> MergeCalls { get; } = [];

    /// <summary>PR numbers that fail with <see cref="MergeNotReadyException"/> on every merge attempt.</summary>
    public HashSet<int> FailForPrNumbers { get; } = [];

    /// <summary>PR numbers that fail with <see cref="MergeNotReadyException"/> only their first merge attempt, then succeed.</summary>
    public HashSet<int> FailOnceForPrNumbers { get; } = [];

    /// <summary>PR numbers that fail with <see cref="GitHubAuthException"/> (insufficient permission) on every merge attempt.</summary>
    public HashSet<int> FailWithAuthErrorForPrNumbers { get; } = [];

    /// <summary>PR numbers that fail with an unrecognized exception (e.g. a 404/422) on every merge attempt.</summary>
    public HashSet<int> FailWithUnexpectedErrorForPrNumbers { get; } = [];

    /// <summary>PR numbers whose first attempt fails as not-ready (entering the poll loop), then whose retry fails with insufficient permission instead of succeeding.</summary>
    public HashSet<int> FailPollRetryWithAuthErrorForPrNumbers { get; } = [];

    public string? FailureMessage { get; set; }

    public Task MergeAsync(string owner, DependabotPr pr, CancellationToken cancellationToken = default)
    {
        MergeCalls.Add((owner, pr));

        if (FailWithAuthErrorForPrNumbers.Contains(pr.Number))
        {
            throw new GitHubAuthException(FailureMessage ?? $"insufficient permission to merge #{pr.Number}");
        }

        if (FailWithUnexpectedErrorForPrNumbers.Contains(pr.Number))
        {
            throw new InvalidOperationException(FailureMessage ?? $"unrecognized response for #{pr.Number}");
        }

        if (FailPollRetryWithAuthErrorForPrNumbers.Contains(pr.Number))
        {
            if (alreadyFailedOnce.Add(pr.Number))
            {
                throw new MergeNotReadyException(FailureMessage ?? $"not mergeable yet on #{pr.Number}");
            }

            throw new GitHubAuthException(FailureMessage ?? $"insufficient permission to merge #{pr.Number}");
        }

        var failsThisCall = FailForPrNumbers.Contains(pr.Number)
            || (FailOnceForPrNumbers.Contains(pr.Number) && alreadyFailedOnce.Add(pr.Number));

        if (failsThisCall)
        {
            throw new MergeNotReadyException(FailureMessage ?? $"not mergeable yet on #{pr.Number}");
        }

        return Task.CompletedTask;
    }
}
