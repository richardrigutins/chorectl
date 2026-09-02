using Chorectl.Core.Update;

namespace Chorectl.Core.Tests.Update;

public class VersionComparerTests
{
    [Theory]
    [InlineData("v1.2.4", "1.2.3")]
    [InlineData("v1.3.0", "1.2.3")]
    [InlineData("v2.0.0", "1.2.3")]
    [InlineData("1.2.4", "v1.2.3")]
    public void IsNewer_WhenCandidateIsGreater_ReturnsTrue(string candidate, string current)
    {
        Assert.True(VersionComparer.IsNewer(candidate, current));
    }

    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("v1.2.2", "1.2.3")]
    [InlineData("v1.0.0", "1.2.3")]
    public void IsNewer_WhenCandidateIsNotGreater_ReturnsFalse(string candidate, string current)
    {
        Assert.False(VersionComparer.IsNewer(candidate, current));
    }

    [Theory]
    [InlineData("not-a-version", "1.2.3")]
    [InlineData("1.2.3", "not-a-version")]
    public void IsNewer_WhenEitherVersionIsUnparseable_ReturnsFalse(string candidate, string current)
    {
        Assert.False(VersionComparer.IsNewer(candidate, current));
    }
}
