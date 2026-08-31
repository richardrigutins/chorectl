using Chorectl.Core.Config;
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
        bool isGrouped = false,
        bool isSecurityUpdate = false,
        string? body = null) => new()
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
            IsSecurityUpdate = isSecurityUpdate,
            Body = body,
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

        Assert.Equal(expected, Classifier.DefaultSelected(pr, new DefaultSelectConfig()));
    }

    [Theory]
    [InlineData(SemverLevel.Patch)]
    [InlineData(SemverLevel.Minor)]
    public void DefaultSelected_NeverSelectsSecurityUpdates_RegardlessOfSemverLevel(SemverLevel semverLevel)
    {
        var pr = CreatePr(semverLevel: semverLevel, isSecurityUpdate: true);

        Assert.False(Classifier.DefaultSelected(pr, new DefaultSelectConfig()));
    }

    [Fact]
    public void DefaultSelected_ReturnsFalse_WhenNotReadyToMerge_EvenIfPatch()
    {
        var pr = CreatePr(mergeStateStatus: "DIRTY", semverLevel: SemverLevel.Patch);

        Assert.False(Classifier.DefaultSelected(pr, new DefaultSelectConfig()));
    }

    [Fact]
    public void DefaultSelected_WithMajorEnabledInConfig_SelectsMajorBumps()
    {
        var pr = CreatePr(semverLevel: SemverLevel.Major);

        Assert.True(Classifier.DefaultSelected(pr, new DefaultSelectConfig { Major = true }));
    }

    [Fact]
    public void DefaultSelected_WithGroupedEnabledInConfig_SelectsGroupedPrs()
    {
        var pr = CreatePr(isGrouped: true);

        Assert.True(Classifier.DefaultSelected(pr, new DefaultSelectConfig { Grouped = true }));
    }

    [Fact]
    public void DefaultSelected_WithPatchDisabledInConfig_ExcludesPatchBumps()
    {
        var pr = CreatePr(semverLevel: SemverLevel.Patch);

        Assert.False(Classifier.DefaultSelected(pr, new DefaultSelectConfig { Patch = false }));
    }

    [Fact]
    public void DefaultSelected_NeverSelectsUnknownSemverLevel_EvenWithMajorEnabledInConfig()
    {
        var pr = CreatePr(semverLevel: SemverLevel.Unknown);

        Assert.False(Classifier.DefaultSelected(pr, new DefaultSelectConfig { Major = true }));
    }

    [Fact]
    public void DefaultSelected_NeverSelectsSecurityUpdates_EvenWithMajorEnabledInConfig()
    {
        var pr = CreatePr(semverLevel: SemverLevel.Major, isSecurityUpdate: true);

        Assert.False(Classifier.DefaultSelected(pr, new DefaultSelectConfig { Major = true }));
    }

    [Theory]
    [InlineData(SemverLevel.Patch, false, true)]
    [InlineData(SemverLevel.Minor, false, true)]
    [InlineData(SemverLevel.Major, false, false)]
    [InlineData(SemverLevel.Unknown, false, false)]
    [InlineData(SemverLevel.Patch, true, false)]
    [InlineData(SemverLevel.Minor, true, false)]
    public void DefaultSelectedForApproval_SelectsPatchAndMinorButNotMajorUnknownOrGrouped(
        SemverLevel semverLevel, bool isGrouped, bool expected)
    {
        var pr = CreatePr(semverLevel: semverLevel, isGrouped: isGrouped);

        Assert.Equal(expected, Classifier.DefaultSelectedForApproval(pr, new DefaultSelectConfig()));
    }

    [Fact]
    public void DefaultSelectedForApproval_NeverSelectsSecurityUpdates_EvenAtPatchLevel()
    {
        var pr = CreatePr(semverLevel: SemverLevel.Patch, isSecurityUpdate: true);

        Assert.False(Classifier.DefaultSelectedForApproval(pr, new DefaultSelectConfig()));
    }

    [Fact]
    public void DefaultSelectedForApproval_DoesNotRequireReadyToMerge_UnlikeDefaultSelected()
    {
        // Every PR reaching the approve screen has ReviewStatus.ReviewRequired, which
        // IsReadyToMerge (and therefore DefaultSelected) would disqualify outright.
        var pr = CreatePr(review: ReviewStatus.ReviewRequired, semverLevel: SemverLevel.Patch);

        Assert.False(Classifier.DefaultSelected(pr, new DefaultSelectConfig()));
        Assert.True(Classifier.DefaultSelectedForApproval(pr, new DefaultSelectConfig()));
    }

    [Fact]
    public void DefaultSelectedForApproval_WithMajorEnabledInConfig_SelectsMajorBumps()
    {
        var pr = CreatePr(semverLevel: SemverLevel.Major);

        Assert.True(Classifier.DefaultSelectedForApproval(pr, new DefaultSelectConfig { Major = true }));
    }

    [Fact]
    public void DefaultSelectedForApproval_WithGroupedEnabledInConfig_SelectsGroupedPrs()
    {
        var pr = CreatePr(isGrouped: true);

        Assert.True(Classifier.DefaultSelectedForApproval(pr, new DefaultSelectConfig { Grouped = true }));
    }

    [Fact]
    public void IsReadyToMerge_AndNeedsRebase_TreatAnUnrecognizedMergeStateStatusLikeClean()
    {
        // MergeStateStatus is a raw GitHub API passthrough, not a closed set on our side - a
        // future value GitHub hasn't introduced yet should degrade gracefully (behave like a
        // non-blocking state) rather than the tool breaking on it.
        var pr = CreatePr(mergeStateStatus: "SOME_FUTURE_STATE_GITHUB_HASNT_INVENTED_YET");

        Assert.True(Classifier.IsReadyToMerge(pr));
        Assert.False(Classifier.NeedsRebase(pr));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("Just a regular PR description.", false)]
    [InlineData("Dependabot is rebasing this PR due to a merge conflict.", true)]
    [InlineData("dependabot IS REBASING THIS pull request right now", true)]
    [InlineData("Heads up: Dependabot is currently rebasing this PR, changes may be lost.", true)]
    public void HasRebaseBanner_LooselyMatchesDependabotsRebasingBannerText(string? body, bool expected)
    {
        var pr = CreatePr(body: body);

        Assert.Equal(expected, Classifier.HasRebaseBanner(pr));
    }
}
