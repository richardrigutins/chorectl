using Chorectl.Core.GitHub;

namespace Chorectl.Core.Tests.GitHub;

public class GitHubHostTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("github.com")]
    [InlineData("GitHub.Com")]
    public void IsDefault_ForGithubCom_ReturnsTrue(string? host)
    {
        Assert.True(GitHubHost.IsDefault(host));
    }

    [Fact]
    public void IsDefault_ForAnEnterpriseHost_ReturnsFalse()
    {
        Assert.False(GitHubHost.IsDefault("github.mycompany.com"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("github.com")]
    public void RestApiBaseUri_ForGithubCom_ReturnsNull(string? host)
    {
        Assert.Null(GitHubHost.RestApiBaseUri(host));
    }

    [Fact]
    public void RestApiBaseUri_ForAnEnterpriseHost_ReturnsApiV3Uri()
    {
        Assert.Equal(new Uri("https://github.mycompany.com/api/v3/"), GitHubHost.RestApiBaseUri("github.mycompany.com"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("github.com")]
    public void GraphQlBaseUri_ForGithubCom_ReturnsApiGithubCom(string? host)
    {
        Assert.Equal(new Uri("https://api.github.com/"), GitHubHost.GraphQlBaseUri(host));
    }

    [Fact]
    public void GraphQlBaseUri_ForAnEnterpriseHost_ReturnsApiUri()
    {
        Assert.Equal(new Uri("https://github.mycompany.com/api/"), GitHubHost.GraphQlBaseUri("github.mycompany.com"));
    }
}
