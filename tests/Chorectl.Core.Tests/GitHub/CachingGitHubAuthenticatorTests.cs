using Chorectl.Core.GitHub;

namespace Chorectl.Core.Tests.GitHub;

public class CachingGitHubAuthenticatorTests
{
    [Fact]
    public void GetToken_CallsTheInnerAuthenticatorOnlyOnce_AcrossRepeatedCalls()
    {
        var inner = new FakeGitHubAuthenticator(() => "gho_token");
        var caching = new CachingGitHubAuthenticator(inner);

        var first = caching.GetToken();
        var second = caching.GetToken();
        var third = caching.GetToken();

        Assert.Equal("gho_token", first);
        Assert.Equal("gho_token", second);
        Assert.Equal("gho_token", third);
        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public void GetToken_WhenTheInnerAuthenticatorThrows_PropagatesTheException()
    {
        var inner = new FakeGitHubAuthenticator(() => throw new GitHubAuthException("not authenticated"));
        var caching = new CachingGitHubAuthenticator(inner);

        Assert.Throws<GitHubAuthException>(caching.GetToken);
    }
}
