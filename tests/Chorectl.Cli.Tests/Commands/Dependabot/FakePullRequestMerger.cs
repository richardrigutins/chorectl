using Chorectl.Core.Domain.Dependabot;
using Chorectl.Core.GitHub;

namespace Chorectl.Cli.Tests.Commands.Dependabot;

internal sealed class FakePullRequestMerger : IPullRequestMerger
{
    public List<(string Owner, DependabotPr Pr)> MergeCalls { get; } = [];

    public HashSet<int> FailForPrNumbers { get; } = [];

    public string? FailureMessage { get; set; }

    public Task MergeAsync(string owner, DependabotPr pr, CancellationToken cancellationToken = default)
    {
        MergeCalls.Add((owner, pr));

        if (FailForPrNumbers.Contains(pr.Number))
        {
            throw new InvalidOperationException(FailureMessage ?? $"merge conflict on #{pr.Number}");
        }

        return Task.CompletedTask;
    }
}
