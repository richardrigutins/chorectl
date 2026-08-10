using Chorectl.Core.GitHub;

namespace Chorectl.Core.Tests.GitHub;

internal sealed class FakeGitHubAuthenticator(Func<string> getToken) : IGitHubAuthenticator
{
    public int CallCount { get; private set; }

    public string GetToken()
    {
        CallCount++;
        return getToken();
    }
}
