using AscendantEmpire.Application.Features.Settlements.Classes;
using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Tests.Features.Settlements;

/// <summary>
/// Tests for <see cref="SettlementHarvestCalculator"/> and <see cref="SettlementWorkAllocation"/>: how much of one resource a Settlement takes off one of its territory tiles in a Round.
/// </summary>
/// <remarks>
/// No database - both are pure. Which tiles are harvested, and where what is taken is banked, are the start-of-Round process's to decide and are covered in
/// <c>HarvestSettlementTerritoryTests</c>. The figures are the design doc's grassland worked example: Forage 40 and Hunt 70 at a Regeneration Percent of 25%, so a tile
/// regrows 10 Forage and 17.5 Hunt a Round.
/// </remarks>
[TestFixture]
internal class SettlementHarvestCalculatorTests
{
    private const int _POPULATION = 100;

    private static readonly ScaledAmount _HUNT_AVAILABLE = ScaledAmount.FromWhole(70);

    private static readonly ScaledAmount _HUNT_REGROWTH = ScaledAmount.FromRaw(17_500);

    #region Sustenance

    [Test, Description("Pins that the regrowth is the cap, so a settlement on a full tile takes what the tile will give back and leaves the standing stock for Forces.")]
    public void ShouldTakeTheRegrowthWhenTheTileHoldsMoreThanThat()
    {
        ScaledAmount take = SettlementHarvestCalculator.CalculateSustenanceTake(_HUNT_AVAILABLE, _HUNT_REGROWTH, SettlementWorkAllocation.CiviliansOnTile(_POPULATION));

        Assert.That(take, Is.EqualTo(_HUNT_REGROWTH));
    }

    [Test, Description("Pins that a tile with less left than it regrows gives only what is left, so a stripped tile cannot be overdrawn.")]
    public void ShouldTakeWhatIsAvailableWhenItIsLessThanTheRegrowth()
    {
        ScaledAmount available = ScaledAmount.FromWhole(5);

        ScaledAmount take = SettlementHarvestCalculator.CalculateSustenanceTake(available, _HUNT_REGROWTH, SettlementWorkAllocation.CiviliansOnTile(_POPULATION));

        Assert.That(take, Is.EqualTo(available));
    }

    [Test, Description("Pins that too few civilians to work the tile is what binds, which is the hook the future per-tile allocation hangs on.")]
    public void ShouldTakeTheStaffedShareWhenItIsTheSmallestOfTheThree()
    {
        ScaledAmount take = SettlementHarvestCalculator.CalculateSustenanceTake(_HUNT_AVAILABLE, _HUNT_REGROWTH, civilians: 10);

        Assert.That(take, Is.EqualTo(ScaledAmount.FromWhole(10 * SettlementHarvestCalculator.CIVILIAN_HARVEST_RATE)));
    }

    [Test, Description("Pins that a settlement with nobody in it harvests nothing, rather than falling back on what the tile holds.")]
    public void ShouldTakeNothingWhenThePopulationIsZero()
    {
        ScaledAmount take = SettlementHarvestCalculator.CalculateSustenanceTake(_HUNT_AVAILABLE, _HUNT_REGROWTH, SettlementWorkAllocation.CiviliansOnTile(0));

        Assert.That(take, Is.EqualTo(ScaledAmount.Zero));
    }

    [Test, Description("Pins that a tile stripped past the owner's own value yields nothing and never a negative amount.")]
    public void ShouldTakeNothingWhenNothingIsAvailable()
    {
        ScaledAmount take = SettlementHarvestCalculator.CalculateSustenanceTake(ScaledAmount.Zero, _HUNT_REGROWTH, SettlementWorkAllocation.CiviliansOnTile(_POPULATION));

        Assert.That(take, Is.EqualTo(ScaledAmount.Zero));
    }

    [Test, Description("Pins that a resource the biome does not have is not harvested at all, which is a different thing from a tile that has none left.")]
    public void ShouldTakeNothingWhenTheResourceIsAbsentFromTheBiome()
    {
        ScaledAmount take = SettlementHarvestCalculator.CalculateSustenanceTake(available: null, _HUNT_REGROWTH, SettlementWorkAllocation.CiviliansOnTile(_POPULATION));

        Assert.That(take, Is.EqualTo(ScaledAmount.Zero));
    }

    [Test, Description("Pins that a biome with no Regeneration Percent yields nothing, since without a regrowth there is no cap to harvest under.")]
    public void ShouldTakeNothingWhenThereIsNoRegrowth()
    {
        ScaledAmount take = SettlementHarvestCalculator.CalculateSustenanceTake(_HUNT_AVAILABLE, regrowth: null, SettlementWorkAllocation.CiviliansOnTile(_POPULATION));

        Assert.That(take, Is.EqualTo(ScaledAmount.Zero));
    }

    #endregion

    #region Materials

    [Test, Description("Pins that a material is taken at the biome's base value every Round, since nothing depletes or regrows it.")]
    public void ShouldTakeTheWholeBaseValueWhenTheBiomeHasTheMaterial()
    {
        ScaledAmount baseValue = ScaledAmount.FromWhole(40);

        ScaledAmount take = SettlementHarvestCalculator.CalculateMaterialTake(baseValue, SettlementWorkAllocation.CiviliansOnTile(_POPULATION));

        Assert.That(take, Is.EqualTo(baseValue));
    }

    [Test, Description("Pins that the staffed share caps a material too, so a tiny settlement cannot work a rich seam as if it were large.")]
    public void ShouldCapTheMaterialAtTheStaffedShareWhenThePopulationIsSmall()
    {
        ScaledAmount take = SettlementHarvestCalculator.CalculateMaterialTake(ScaledAmount.FromWhole(40), civilians: 10);

        Assert.That(take, Is.EqualTo(ScaledAmount.FromWhole(10 * SettlementHarvestCalculator.CIVILIAN_HARVEST_RATE)));
    }

    [Test, Description("Pins that a material the biome does not have is not harvested, so an absent seam banks nothing.")]
    public void ShouldTakeNothingWhenTheBiomeHasNoValueForTheMaterial()
    {
        ScaledAmount take = SettlementHarvestCalculator.CalculateMaterialTake(baseValue: null, SettlementWorkAllocation.CiviliansOnTile(_POPULATION));

        Assert.That(take, Is.EqualTo(ScaledAmount.Zero));
    }

    #endregion

    #region Work allocation

    [Test, Description("Pins the current simplification - the whole population works every tile - which is the one thing a per-tile allocation would replace.")]
    public void ShouldStaffATileWithTheWholePopulationWhenTheSettlementIsInhabited()
    {
        Assert.That(SettlementWorkAllocation.CiviliansOnTile(_POPULATION), Is.EqualTo(_POPULATION));
    }

    [Test, Description("Pins that a population below zero staffs nothing, so corrupt data cannot produce a negative harvest.")]
    public void ShouldStaffNobodyWhenThePopulationIsNegative()
    {
        Assert.That(SettlementWorkAllocation.CiviliansOnTile(-5), Is.EqualTo(0));
    }

    #endregion
}
