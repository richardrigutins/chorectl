using Chorectl.Core.Domain.Dependabot;
using Chorectl.Core.GitHub;

namespace Chorectl.Cli.Tests.Commands.Dependabot;

internal sealed class FakePullRequestCommenter : IPullRequestCommenter
{
    private readonly HashSet<int> alreadyFailedOnce = [];

    public List<(string Owner, DependabotPr Pr, string Body)> CommentCalls { get; } = [];

    /// <summary>PR numbers that fail with <see cref="GitHubAuthException"/> (insufficient permission).</summary>
    public HashSet<int> FailWithAuthErrorForPrNumbers { get; } = [];

    /// <summary>PR numbers that fail with <see cref="GitHubRateLimitException"/> on every attempt.</summary>
    public HashSet<int> FailWithRateLimitErrorForPrNumbers { get; } = [];

    /// <summary>PR numbers that fail with <see cref="GitHubRateLimitException"/> only their first attempt, then succeed.</summary>
    public HashSet<int> RateLimitOnceThenSucceedForPrNumbers { get; } = [];

    /// <summary>PR numbers that fail with an unrecognized exception (e.g. a 404/422).</summary>
    public HashSet<int> FailWithUnexpectedErrorForPrNumbers { get; } = [];

    public string? FailureMessage { get; set; }

    public Task CommentAsync(string owner, DependabotPr pr, string body, CancellationToken cancellationToken = default)
    {
        CommentCalls.Add((owner, pr, body));

        if (FailWithAuthErrorForPrNumbers.Contains(pr.Number))
        {
            throw new GitHubAuthException(FailureMessage ?? $"insufficient permission to comment on #{pr.Number}");
        }

        if (FailWithRateLimitErrorForPrNumbers.Contains(pr.Number)
            || (RateLimitOnceThenSucceedForPrNumbers.Contains(pr.Number) && alreadyFailedOnce.Add(pr.Number)))
        {
            throw new GitHubRateLimitException(FailureMessage ?? $"rate limited on #{pr.Number}");
        }

        if (FailWithUnexpectedErrorForPrNumbers.Contains(pr.Number))
        {
            throw new InvalidOperationException(FailureMessage ?? $"unrecognized response for #{pr.Number}");
        }

        return Task.CompletedTask;
    }
}
