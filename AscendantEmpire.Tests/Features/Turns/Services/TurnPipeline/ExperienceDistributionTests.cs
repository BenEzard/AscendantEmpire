using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndTurn;

namespace AscendantEmpire.Tests.Features.Turns.Services.TurnPipeline;

/// <summary>
/// Tests for <see cref="ExperienceDistribution"/> — how one Turn's accrued experience divides between the technologies that consume it.
/// </summary>
/// <remarks>
/// No database, the same tier and for the same reason as <see cref="ResearchBucketFillTests"/>: the rule is a pure function of the accruals and the recipient
/// snapshot, so the whole table of cases is pinned without standing anything up. What <c>ApplyResearch</c> does with the result is pinned separately in
/// <see cref="ApplyResearchTests"/>.
/// </remarks>
[TestFixture]
internal class ExperienceDistributionTests
{
    private const int _JUNGLE_TYPE_ID = 1;
    private const int _MINING_TYPE_ID = 2;

    private const int _FIRST_TECHNOLOGY_ID = 10;
    private const int _SECOND_TECHNOLOGY_ID = 20;

    #region Even split

    [Test, Description("A type's points divide evenly between the technologies that consume it, which is the rule technology.md sets for the experience bucket.")]
    public void ShouldSplitEvenlyWhenEveryRecipientHasHeadroom()
    {
        ExperienceSplit split = ExperienceDistribution.Split(
            PointsFor(_JUNGLE_TYPE_ID, 10),
            [
                Recipient(_FIRST_TECHNOLOGY_ID, headroom: 100, _JUNGLE_TYPE_ID),
                Recipient(_SECOND_TECHNOLOGY_ID, headroom: 100, _JUNGLE_TYPE_ID)
            ]);

        Assert.Multiple(() =>
        {
            Assert.That(split.PointsByTechnologyId[_FIRST_TECHNOLOGY_ID], Is.EqualTo(5));
            Assert.That(split.PointsByTechnologyId[_SECOND_TECHNOLOGY_ID], Is.EqualTo(5));
            Assert.That(split.UnusedPointsByExperienceTypeId, Is.Empty);
        });
    }

    [Test, Description("The remainder of an uneven division has nowhere to go, so it is recorded as unused rather than favouring whichever recipient is enumerated first.")]
    public void ShouldSendTheRemainderToUnusedWhenTheSplitDoesNotDivideCleanly()
    {
        ExperienceSplit split = ExperienceDistribution.Split(
            PointsFor(_JUNGLE_TYPE_ID, 11),
            [
                Recipient(_FIRST_TECHNOLOGY_ID, headroom: 100, _JUNGLE_TYPE_ID),
                Recipient(_SECOND_TECHNOLOGY_ID, headroom: 100, _JUNGLE_TYPE_ID)
            ]);

        Assert.Multiple(() =>
        {
            Assert.That(split.PointsByTechnologyId[_FIRST_TECHNOLOGY_ID], Is.EqualTo(5));
            Assert.That(split.PointsByTechnologyId[_SECOND_TECHNOLOGY_ID], Is.EqualTo(5));
            Assert.That(split.UnusedPointsByExperienceTypeId[_JUNGLE_TYPE_ID], Is.EqualTo(1));
        });
    }

    #endregion

    #region Nothing to receive it

    [Test, Description("A type no technology consumes still has to be accounted for, so its whole total lands in unused rather than disappearing.")]
    public void ShouldSendTheWholeTotalToUnusedWhenNoTechnologyConsumesTheType()
    {
        ExperienceSplit split = ExperienceDistribution.Split(
            PointsFor(_JUNGLE_TYPE_ID, 7),
            [Recipient(_FIRST_TECHNOLOGY_ID, headroom: 100, _MINING_TYPE_ID)]);

        Assert.Multiple(() =>
        {
            Assert.That(split.PointsByTechnologyId, Is.Empty);
            Assert.That(split.UnusedPointsByExperienceTypeId[_JUNGLE_TYPE_ID], Is.EqualTo(7));
        });
    }

    [Test, Description("Fewer points than recipients gives each of them nothing, and all of it is unused - an even split cannot hand out a fraction of a point.")]
    public void ShouldSendEverythingToUnusedWhenThereAreFewerPointsThanRecipients()
    {
        ExperienceSplit split = ExperienceDistribution.Split(
            PointsFor(_JUNGLE_TYPE_ID, 3),
            [
                Recipient(1, headroom: 100, _JUNGLE_TYPE_ID),
                Recipient(2, headroom: 100, _JUNGLE_TYPE_ID),
                Recipient(3, headroom: 100, _JUNGLE_TYPE_ID),
                Recipient(4, headroom: 100, _JUNGLE_TYPE_ID)
            ]);

        Assert.Multiple(() =>
        {
            Assert.That(split.PointsByTechnologyId, Is.Empty);
            Assert.That(split.UnusedPointsByExperienceTypeId[_JUNGLE_TYPE_ID], Is.EqualTo(3));
        });
    }

    #endregion

    #region Clamping

    [Test, Description("A recipient is clamped at the headroom it had before the drain began, and the surplus is unused, so a bucket can never exceed its requirement.")]
    public void ShouldClampAtHeadroomAndRecordTheSurplusAsUnusedWhenAShareOverfillsABucket()
    {
        ExperienceSplit split = ExperienceDistribution.Split(
            PointsFor(_JUNGLE_TYPE_ID, 10),
            [
                Recipient(_FIRST_TECHNOLOGY_ID, headroom: 2, _JUNGLE_TYPE_ID),
                Recipient(_SECOND_TECHNOLOGY_ID, headroom: 100, _JUNGLE_TYPE_ID)
            ]);

        Assert.Multiple(() =>
        {
            Assert.That(split.PointsByTechnologyId[_FIRST_TECHNOLOGY_ID], Is.EqualTo(2));
            Assert.That(split.PointsByTechnologyId[_SECOND_TECHNOLOGY_ID], Is.EqualTo(5));
            Assert.That(split.UnusedPointsByExperienceTypeId[_JUNGLE_TYPE_ID], Is.EqualTo(3));
        });
    }

    [Test, Description("A technology fed by two types is clamped once against one headroom, so the result cannot depend on which type was applied first.")]
    public void ShouldClampOnceAcrossBothTypesWhenOneTechnologyConsumesTwo()
    {
        ExperienceSplit split = ExperienceDistribution.Split(
            new Dictionary<int, int> { [_JUNGLE_TYPE_ID] = 8, [_MINING_TYPE_ID] = 8 },
            [Recipient(_FIRST_TECHNOLOGY_ID, headroom: 10, _JUNGLE_TYPE_ID, _MINING_TYPE_ID)]);

        Assert.Multiple(() =>
        {
            Assert.That(split.PointsByTechnologyId[_FIRST_TECHNOLOGY_ID], Is.EqualTo(10));
            Assert.That(split.UnusedPointsByExperienceTypeId[_JUNGLE_TYPE_ID], Is.EqualTo(6));
            Assert.That(split.UnusedPointsByExperienceTypeId.ContainsKey(_MINING_TYPE_ID), Is.False);
        });
    }

    [Test, Description("A recipient with no headroom left takes nothing and its share goes to unused rather than off the floor, so a caller that forgets to filter spent technologies loses no points.")]
    public void ShouldSendTheShareOfASpentRecipientToUnused()
    {
        ExperienceSplit split = ExperienceDistribution.Split(
            PointsFor(_JUNGLE_TYPE_ID, 10),
            [
                Recipient(_FIRST_TECHNOLOGY_ID, headroom: 0, _JUNGLE_TYPE_ID),
                Recipient(_SECOND_TECHNOLOGY_ID, headroom: 100, _JUNGLE_TYPE_ID)
            ]);

        Assert.Multiple(() =>
        {
            Assert.That(split.PointsByTechnologyId.ContainsKey(_FIRST_TECHNOLOGY_ID), Is.False);
            Assert.That(split.PointsByTechnologyId[_SECOND_TECHNOLOGY_ID], Is.EqualTo(5));
            Assert.That(split.UnusedPointsByExperienceTypeId[_JUNGLE_TYPE_ID], Is.EqualTo(5), "Everything the spent recipient could not take is still accounted for.");
        });
    }

    #endregion

    #region Helpers

    private static Dictionary<int, int> PointsFor(int experienceTypeId, int points)
    {
        return new Dictionary<int, int> { [experienceTypeId] = points };
    }

    private static ExperienceRecipient Recipient(int technologyId, int headroom, params int[] experienceTypeIds)
    {
        return new ExperienceRecipient(technologyId, headroom, experienceTypeIds.ToHashSet());
    }

    #endregion
}
