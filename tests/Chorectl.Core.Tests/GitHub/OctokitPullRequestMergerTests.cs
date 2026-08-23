using Chorectl.Core.GitHub;
using Octokit;

namespace Chorectl.Core.Tests.GitHub;

public class OctokitPullRequestMergerTests
{
    [Theory]
    [InlineData("squash", PullRequestMergeMethod.Squash)]
    [InlineData("merge", PullRequestMergeMethod.Merge)]
    [InlineData("rebase", PullRequestMergeMethod.Rebase)]
    public void ParseMergeMethod_MapsTheConfiguredValueToTheMatchingOctokitEnum(string mergeMethod, PullRequestMergeMethod expected)
    {
        Assert.Equal(expected, OctokitPullRequestMerger.ParseMergeMethod(mergeMethod));
    }

    [Fact]
    public void ParseMergeMethod_WithAnUnrecognizedValue_FallsBackToSquash()
    {
        Assert.Equal(PullRequestMergeMethod.Squash, OctokitPullRequestMerger.ParseMergeMethod("not-a-real-method"));
    }
}
