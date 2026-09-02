using Chorectl.Core.GitHub;

namespace Chorectl.Core.Tests.Update;

internal sealed class FakeReleaseSource(ReleaseInfo? release = null, Exception? failWith = null, bool neverCompletes = false) : IReleaseSource
{
    public int CallCount { get; private set; }

    public Task<ReleaseInfo> GetLatestReleaseAsync()
    {
        CallCount++;

        if (neverCompletes)
        {
            return new TaskCompletionSource<ReleaseInfo>().Task;
        }

        return failWith is not null
            ? Task.FromException<ReleaseInfo>(failWith)
            : Task.FromResult(release ?? new ReleaseInfo("v1.0.0", []));
    }
}
