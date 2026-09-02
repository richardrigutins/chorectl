using Chorectl.Core.GitHub;

namespace Chorectl.Cli.Tests.Commands;

internal sealed class FakeReleaseSource(ReleaseInfo release) : IReleaseSource
{
    public Task<ReleaseInfo> GetLatestReleaseAsync() => Task.FromResult(release);
}
