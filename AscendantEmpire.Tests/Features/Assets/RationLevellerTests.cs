using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Tests for <see cref="RationLeveller"/>: how much of each thing a Force carries it eats, and what that is worth to it as food.
/// </summary>
/// <remarks>
/// No database - the leveller is pure. The water-line arithmetic under it is pinned in <see cref="WaterLineAllocatorTests"/>; what is pinned here is the
/// conversion between an amount of an Asset and a number of rations, which is where the two measures meet.
/// </remarks>
[TestFixture]
internal class RationLevellerTests
{
    private const int _GRAIN_ASSET_ID = 1;
    private const int _FISH_ASSET_ID = 2;

    /// <summary>One Asset per ration, which is the ordinary case and makes a holding's rations its quantity.</summary>
    private const long _ONE_FOR_ONE = 1_000;

    [Test, Description("Carried Assets of the same price are levelled to the same quantity, so the Force runs out of both at once rather than one first.")]
    public void ShouldLevelEquallyPricedAssetsToTheSameQuantity()
    {
        RationTake take = RationLeveller.Level(
            [Carried(_GRAIN_ASSET_ID, whole: 50, _ONE_FOR_ONE), Carried(_FISH_ASSET_ID, whole: 30, _ONE_FOR_ONE)],
            ScaledAmount.FromWhole(40));

        using (Assert.EnterMultipleScope())
        {
            // 50 and 30 levelled by a take of 40 leaves 20 of each.
            Assert.That(take.TakeRawByAssetId[_GRAIN_ASSET_ID], Is.EqualTo(ScaledAmount.FromWhole(30).RawValue));
            Assert.That(take.TakeRawByAssetId[_FISH_ASSET_ID], Is.EqualTo(ScaledAmount.FromWhole(10).RawValue));
            Assert.That(take.Fed, Is.EqualTo(ScaledAmount.FromWhole(40)));
        }
    }

    [Test, Description("The level is rations rather than quantity, so the Asset covering the most Turns is eaten first however little of it there is.")]
    public void ShouldLevelOnRationsRatherThanQuantity()
    {
        // 50 grain at one each is 50 rations; 30 fish at two each is only 15, so the grain is the richer holding despite the quantities.
        RationTake take = RationLeveller.Level(
            [Carried(_GRAIN_ASSET_ID, whole: 50, _ONE_FOR_ONE), Carried(_FISH_ASSET_ID, whole: 30, amountPerUnit: 2_000)],
            ScaledAmount.FromWhole(30));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(take.TakeRawByAssetId[_GRAIN_ASSET_ID], Is.EqualTo(ScaledAmount.FromWhole(30).RawValue));
            Assert.That(take.TakeRawByAssetId.ContainsKey(_FISH_ASSET_ID), Is.False, "The grain was still the richer holding when the need ran out, so the fish was never touched.");
        }
    }

    [Test, Description("A need beyond everything carried eats the whole load and feeds only what it was worth, which is what leaves the Force still short.")]
    public void ShouldEatTheWholeLoadAndFeedOnlyWhatItIsWorth()
    {
        RationTake take = RationLeveller.Level([Carried(_GRAIN_ASSET_ID, whole: 10, amountPerUnit: 2_000)], ScaledAmount.FromWhole(20));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(take.TakeRawByAssetId[_GRAIN_ASSET_ID], Is.EqualTo(ScaledAmount.FromWhole(10).RawValue));
            Assert.That(take.Fed, Is.EqualTo(ScaledAmount.FromWhole(5)), "Ten at two each is five rations, and the other fifteen are a shortfall.");
        }
    }

    [Test, Description("A need that does not divide into the Asset is covered rather than left a thousandth short, so no individual starves over rounding.")]
    public void ShouldRoundTheTakeUpSoASliverOfANeedIsStillCovered()
    {
        RationTake take = RationLeveller.Level([Carried(_GRAIN_ASSET_ID, whole: 10, amountPerUnit: 1_500)], ScaledAmount.FromRaw(1));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(take.TakeRawByAssetId[_GRAIN_ASSET_ID], Is.EqualTo(2), "A thousandth of a ration costs one and a half, which comes off as two.");
            Assert.That(take.Fed, Is.EqualTo(ScaledAmount.FromRaw(1)), "Overshooting the amount never feeds more than was needed.");
        }
    }

    [Test, Description("An Asset the catalogue prices at nothing is worth nothing as food rather than an unlimited meal, and is left where it is.")]
    public void ShouldIgnoreAnAssetPricedAtNothing()
    {
        RationTake take = RationLeveller.Level([Carried(_GRAIN_ASSET_ID, whole: 50, amountPerUnit: 0)], ScaledAmount.FromWhole(10));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(take.TakeRawByAssetId, Is.Empty);
            Assert.That(take.Fed, Is.EqualTo(ScaledAmount.Zero));
        }
    }

    [Test, Description("The take depends only on the holdings and the need, never on the order they arrive in, so nothing about processing order decides what is eaten.")]
    public void ShouldNotDependOnTheOrderTheHoldingsArriveIn()
    {
        CarriedRation grain = Carried(_GRAIN_ASSET_ID, whole: 50, _ONE_FOR_ONE);
        CarriedRation fish = Carried(_FISH_ASSET_ID, whole: 30, _ONE_FOR_ONE);

        RationTake forwards = RationLeveller.Level([grain, fish], ScaledAmount.FromWhole(40));
        RationTake backwards = RationLeveller.Level([fish, grain], ScaledAmount.FromWhole(40));

        Assert.That(backwards.TakeRawByAssetId, Is.EquivalentTo(forwards.TakeRawByAssetId));
    }

    [Test, Description("A need already met costs the load nothing, so a Force the tile filled keeps everything it is carrying.")]
    public void ShouldTakeNothingWhenTheNeedIsAlreadyMet()
    {
        RationTake take = RationLeveller.Level([Carried(_GRAIN_ASSET_ID, whole: 50, _ONE_FOR_ONE)], ScaledAmount.Zero);

        Assert.That(take.TakeRawByAssetId, Is.Empty);
    }

    #region Helpers

    private static CarriedRation Carried(int assetId, long whole, long amountPerUnit)
    {
        return new CarriedRation(assetId, ScaledAmount.FromWhole(whole).RawValue, amountPerUnit);
    }

    #endregion
}
