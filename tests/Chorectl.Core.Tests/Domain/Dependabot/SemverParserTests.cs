using Chorectl.Core.Domain.Dependabot;

namespace Chorectl.Core.Tests.Domain.Dependabot;

public class SemverParserTests
{
    [Theory]
    [InlineData("Bump firebase-tools from 11.2.0 to 11.3.1", SemverLevel.Minor)]
    [InlineData("Bump lodash from 4.17.20 to 4.17.21", SemverLevel.Patch)]
    [InlineData("Bump @angular/core from 18.1.0 to 19.0.0", SemverLevel.Major)]
    [InlineData("Bump the aws-sdk-go group from 1.2.3 to 1.3.0 in /aws-sdk-go", SemverLevel.Minor)]
    [InlineData("Bump foo from 1.2.3-beta.1 to 1.2.3-beta.2", SemverLevel.Patch)]
    [InlineData("Bump actions/checkout from v3 to v4", SemverLevel.Major)]
    [InlineData("Bump actions/setup-node from v4 to v4", SemverLevel.Patch)]
    public void Classify_ParsesDependabotTitleAndDiffsVersions(string title, SemverLevel expected)
    {
        Assert.Equal(expected, SemverParser.Classify(title));
    }

    [Theory]
    [InlineData("Bump django from 20230101 to 20230201")] // date-based version, not semver
    [InlineData("Not a dependabot title at all")]
    [InlineData("")]
    public void Classify_ReturnsUnknown_WhenTitleOrVersionsArentParseable(string title)
    {
        Assert.Equal(SemverLevel.Unknown, SemverParser.Classify(title));
    }
}
