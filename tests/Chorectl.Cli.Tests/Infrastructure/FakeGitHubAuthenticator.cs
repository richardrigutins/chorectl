using Chorectl.Core.GitHub;

namespace Chorectl.Cli.Tests.Infrastructure;

internal sealed class FakeGitHubAuthenticator(Exception? failWith = null) : IGitHubAuthenticator
{
    public bool WasCalled { get; private set; }

    public string GetToken()
    {
        WasCalled = true;

        if (failWith is not null)
        {
            throw failWith;
        }

        return "fake-token";
    }
}
