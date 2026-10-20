using AscendantEmpire.Application.Features.Maps.Biomes.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Maps.Biomes;

/// <summary>
/// Tests for <see cref="RiverSustenanceBonus"/>: what the rivers on a tile's edges add to its Forage, Hunt, Fish and Fodder.
/// </summary>
/// <remarks>
/// No database - the rule is pure. Every case builds the four edges directly, since which tile owns which edge is settled in <c>TileRiverDerivationTests</c> and is not
/// this class's business.
/// </remarks>
[TestFixture]
internal class RiverSustenanceBonusTests
{
    /// <summary>
    /// A tile with no rivers at all.
    /// </summary>
    private static readonly TileRivers _NO_RIVERS = TileRivers.None;

    [Test, Description("A single Medium edge adds 20% to Hunt, so 200 becomes 240.")]
    public void ShouldAddTwentyPercentToHuntForOneEdge()
    {
        ScaledAmount? hunt = RiverSustenanceBonus.Apply(ScaledAmount.FromWhole(200), SustenanceResource.Hunt, Rivers(north: RiverWidth.Medium), isWater: false);

        Assert.That(hunt, Is.EqualTo(ScaledAmount.FromWhole(240)));
    }

    [Test, Description("The percentages sum across the edges rather than compounding: two edges on 200 Fodder give 280, not 288.")]
    public void ShouldSumThePercentageAcrossEdgesForFodder()
    {
        ScaledAmount? fodder = RiverSustenanceBonus.Apply(
            ScaledAmount.FromWhole(200),
            SustenanceResource.Fodder,
            Rivers(north: RiverWidth.Medium, east: RiverWidth.Large),
            isWater: false);

        Assert.That(fodder, Is.EqualTo(ScaledAmount.FromWhole(280)));
    }

    [Test, Description("The percentage ignores the river's width: a Large edge is worth the same 20% to Hunt as a Medium one.")]
    public void ShouldIgnoreTheWidthForThePercentage()
    {
        ScaledAmount? medium = RiverSustenanceBonus.Apply(ScaledAmount.FromWhole(100), SustenanceResource.Hunt, Rivers(north: RiverWidth.Medium), isWater: false);
        ScaledAmount? large = RiverSustenanceBonus.Apply(ScaledAmount.FromWhole(100), SustenanceResource.Hunt, Rivers(north: RiverWidth.Large), isWater: false);

        Assert.That(medium, Is.EqualTo(large));
    }

    [TestCase(RiverWidth.Medium, 15, Description = "A Medium edge is worth 15 Fish.")]
    [TestCase(RiverWidth.Large, 30, Description = "A Large edge is worth 30 Fish.")]
    public void ShouldAddTheFlatFishForOneEdge(RiverWidth width, int expectedFish)
    {
        ScaledAmount? fish = RiverSustenanceBonus.Apply(value: null, SustenanceResource.Fish, Rivers(south: width), isWater: false);

        Assert.That(fish, Is.EqualTo(ScaledAmount.FromWhole(expectedFish)));
    }

    [Test, Description("The Fish of every edge adds up by width: one Medium and one Large edge give 15 + 30 = 45.")]
    public void ShouldAddTheFishOfEveryEdgeTogether()
    {
        ScaledAmount? fish = RiverSustenanceBonus.Apply(
            value: null,
            SustenanceResource.Fish,
            Rivers(north: RiverWidth.Medium, west: RiverWidth.Large),
            isWater: false);

        Assert.That(fish, Is.EqualTo(ScaledAmount.FromWhole(45)));
    }

    [Test, Description("A biome that already has Fish keeps it and gains the river's on top, rather than being replaced by it.")]
    public void ShouldAddTheRiverFishOnTopOfABiomesOwn()
    {
        ScaledAmount? fish = RiverSustenanceBonus.Apply(ScaledAmount.FromWhole(100), SustenanceResource.Fish, Rivers(east: RiverWidth.Large), isWater: false);

        Assert.That(fish, Is.EqualTo(ScaledAmount.FromWhole(130)));
    }

    [Test, Description("A river does not create the percentage resources the biome lacks: a null Forage, Hunt or Fodder stays null however many edges there are.")]
    public void ShouldLeaveThePercentageResourcesNullWhenTheBiomeHasNone()
    {
        TileRivers rivers = Rivers(north: RiverWidth.Large, south: RiverWidth.Large);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(RiverSustenanceBonus.Apply(value: null, SustenanceResource.Forage, rivers, isWater: false), Is.Null);
            Assert.That(RiverSustenanceBonus.Apply(value: null, SustenanceResource.Hunt, rivers, isWater: false), Is.Null);
            Assert.That(RiverSustenanceBonus.Apply(value: null, SustenanceResource.Fodder, rivers, isWater: false), Is.Null);
        }
    }

    [Test, Description("Forage takes the same 20% an edge gives Hunt and Fodder, so 400 becomes 480 on a one-river bank.")]
    public void ShouldAddTwentyPercentToForageForOneEdge()
    {
        ScaledAmount? forage = RiverSustenanceBonus.Apply(ScaledAmount.FromWhole(400), SustenanceResource.Forage, Rivers(north: RiverWidth.Large), isWater: false);

        Assert.That(forage, Is.EqualTo(ScaledAmount.FromWhole(480)));
    }

    [Test, Description("A water tile gains nothing from a river running into it: the sea is no richer at a river mouth.")]
    public void ShouldLeaveAWaterTileAlone()
    {
        TileRivers rivers = Rivers(north: RiverWidth.Large);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(RiverSustenanceBonus.Apply(ScaledAmount.FromWhole(300), SustenanceResource.Fish, rivers, isWater: true), Is.EqualTo(ScaledAmount.FromWhole(300)));
            Assert.That(RiverSustenanceBonus.Apply(ScaledAmount.FromWhole(200), SustenanceResource.Hunt, rivers, isWater: true), Is.EqualTo(ScaledAmount.FromWhole(200)));
        }
    }

    [Test, Description("A land tile with no river is untouched, Fish included, so dry ground is never given fish by this rule.")]
    public void ShouldLeaveARiverlessLandTileAlone()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(RiverSustenanceBonus.Apply(value: null, SustenanceResource.Fish, _NO_RIVERS, isWater: false), Is.Null);
            Assert.That(RiverSustenanceBonus.Apply(ScaledAmount.FromWhole(200), SustenanceResource.Hunt, _NO_RIVERS, isWater: false), Is.EqualTo(ScaledAmount.FromWhole(200)));
        }
    }

    [Test, Description("Rivers that are not known - a null, as the callers pass when a map's geometry could not be read - are read as no rivers.")]
    public void ShouldTreatUnknownRiversAsNone()
    {
        ScaledAmount? hunt = RiverSustenanceBonus.Apply(ScaledAmount.FromWhole(200), SustenanceResource.Hunt, rivers: null, isWater: false);

        Assert.That(hunt, Is.EqualTo(ScaledAmount.FromWhole(200)));
    }

    [Test, Description("The raw overload the regrowth readers use gives the same answer as the scaled one, so a riverbank regrows against what it holds.")]
    public void ShouldApplyTheSameRuleToARawBaseValue()
    {
        ScaledAmount? fromRaw = RiverSustenanceBonus.Apply(ScaledAmount.FromWhole(200).RawValue, SustenanceResource.Hunt, Rivers(north: RiverWidth.Medium), isWater: false);

        Assert.That(fromRaw, Is.EqualTo(ScaledAmount.FromWhole(240)));
    }

    /// <summary>
    /// A tile's four edges, every one of them riverless unless named.
    /// </summary>
    private static TileRivers Rivers(
        RiverWidth north = RiverWidth.None,
        RiverWidth east = RiverWidth.None,
        RiverWidth south = RiverWidth.None,
        RiverWidth west = RiverWidth.None)
    {
        return new TileRivers { North = north, East = east, South = south, West = west };
    }
}
