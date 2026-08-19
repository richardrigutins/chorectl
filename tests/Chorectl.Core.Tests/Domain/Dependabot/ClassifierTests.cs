using Chorectl.Core.Domain.Dependabot;

namespace Chorectl.Core.Tests.Domain.Dependabot;

public class ClassifierTests
{
    private static DependabotPr CreatePr(
        CiStatus ci = CiStatus.Passing,
        ReviewStatus review = ReviewStatus.NotRequired,
        string mergeStateStatus = "CLEAN",
        bool isDraft = false,
        SemverLevel semverLevel = SemverLevel.Patch,
        bool isGrouped = false) => new()
        {
            Repo = "repo",
            Number = 1,
            Title = "Bump foo from 1.0.0 to 1.0.1",
            Url = "https://github.com/owner/repo/pull/1",
            HeadRefName = "dependabot/npm_and_yarn/foo-1.0.1",
            Ci = ci,
            Review = review,
            MergeStateStatus = mergeStateStatus,
            IsDraft = isDraft,
            SemverLevel = semverLevel,
            IsGrouped = isGrouped,
        };

    [Theory]
    [InlineData(CiStatus.Passing, ReviewStatus.NotRequired, "CLEAN", false, true)]
    [InlineData(CiStatus.Passing, ReviewStatus.Approved, "CLEAN", false, true)]
    [InlineData(CiStatus.Passing, ReviewStatus.ReviewRequired, "CLEAN", false, false)]
    [InlineData(CiStatus.Failing, ReviewStatus.NotRequired, "CLEAN", false, false)]
    [InlineData(CiStatus.Pending, ReviewStatus.NotRequired, "CLEAN", false, false)]
    [InlineData(CiStatus.NoChecks, ReviewStatus.NotRequired, "CLEAN", false, false)]
    [InlineData(CiStatus.Passing, ReviewStatus.NotRequired, "DIRTY", false, false)]
    [InlineData(CiStatus.Passing, ReviewStatus.NotRequired, "BEHIND", false, true)]
    [InlineData(CiStatus.Passing, ReviewStatus.NotRequired, "BLOCKED", false, true)]
    [InlineData(CiStatus.Passing, ReviewStatus.NotRequired, "UNSTABLE", false, true)]
    [InlineData(CiStatus.Passing, ReviewStatus.NotRequired, "UNKNOWN", false, true)]
    [InlineData(CiStatus.Passing, ReviewStatus.NotRequired, "CLEAN", true, false)]
    public void IsReadyToMerge_RequiresPassingCiNoRequiredReviewNotDraftAndNotConflicting(
        CiStatus ci, ReviewStatus review, string mergeStateStatus, bool isDraft, bool expected)
    {
        var pr = CreatePr(ci: ci, review: review, mergeStateStatus: mergeStateStatus, isDraft: isDraft);

        Assert.Equal(expected, Classifier.IsReadyToMerge(pr));
    }

    [Theory]
    [InlineData("DIRTY", true)]
    [InlineData("BEHIND", true)]
    [InlineData("CLEAN", false)]
    [InlineData("BLOCKED", false)]
    [InlineData("UNSTABLE", false)]
    [InlineData("UNKNOWN", false)]
    public void NeedsRebase_IsTrueOnlyForDirtyOrBehind(string mergeStateStatus, bool expected)
    {
        var pr = CreatePr(mergeStateStatus: mergeStateStatus);

        Assert.Equal(expected, Classifier.NeedsRebase(pr));
    }

    [Theory]
    [InlineData(ReviewStatus.ReviewRequired, true)]
    [InlineData(ReviewStatus.Approved, false)]
    [InlineData(ReviewStatus.NotRequired, false)]
    public void NeedsApproval_IsTrueOnlyWhenReviewIsRequired(ReviewStatus review, bool expected)
    {
        var pr = CreatePr(review: review);

        Assert.Equal(expected, Classifier.NeedsApproval(pr));
    }

    [Theory]
    [InlineData(SemverLevel.Patch, false, true)]
    [InlineData(SemverLevel.Minor, false, true)]
    [InlineData(SemverLevel.Major, false, false)]
    [InlineData(SemverLevel.Unknown, false, false)]
    [InlineData(SemverLevel.Patch, true, false)]
    [InlineData(SemverLevel.Minor, true, false)]
    public void DefaultSelected_WhenReady_SelectsPatchAndMinorButNotMajorUnknownOrGrouped(
        SemverLevel semverLevel, bool isGrouped, bool expected)
    {
        var pr = CreatePr(semverLevel: semverLevel, isGrouped: isGrouped);

        Assert.Equal(expected, Classifier.DefaultSelected(pr));
    }

    [Fact]
    public void DefaultSelected_ReturnsFalse_WhenNotReadyToMerge_EvenIfPatch()
    {
        var pr = CreatePr(mergeStateStatus: "DIRTY", semverLevel: SemverLevel.Patch);

        Assert.False(Classifier.DefaultSelected(pr));
    }
}
