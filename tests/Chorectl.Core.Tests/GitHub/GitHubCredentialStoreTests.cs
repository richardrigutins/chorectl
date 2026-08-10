using Chorectl.Core.GitHub;

namespace Chorectl.Core.Tests.GitHub;

public class GitHubCredentialStoreTests
{
    [Fact]
    public async Task GetCredentials_ReturnsCredentialsWrappingTheAuthenticatorsToken()
    {
        var authenticator = new FakeGitHubAuthenticator(() => "gho_token");
        var store = new GitHubCredentialStore(authenticator);

        var credentials = await store.GetCredentials();

        Assert.Equal("gho_token", credentials.Password);
        Assert.Equal(1, authenticator.CallCount);
    }
}
