using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Structures.Services;
using AscendantEmpire.BackendContracts.Structures;
using AscendantEmpire.Domain.Enums.Structures;

namespace AscendantEmpire.Tests.Features.DatabaseSeeding;

/// <summary>
/// Tests for <see cref="StructureSeedData"/>: the authored shape of the Structure catalogue.
/// </summary>
/// <remarks>
/// No database. The codes here are load-bearing in two places outside this file - the upsert matches rows on them, and the client's artwork is keyed on them - so what is
/// pinned is that they are unique and that nothing in the catalogue would break the construction step.
/// </remarks>
[TestFixture]
internal class StructureSeedDataTests
{
    /// <summary>The human body's Power, as <c>UnitComponentSeedData</c> authors it: 7.0, raw Scale-1,000.</summary>
    private const long _HUMAN_BODY_POWER_RAW = 7_000;

    /// <summary>The human body's campaign movement allowance, as authored: 1.0, raw Scale-1,000.</summary>
    private const long _HUMAN_CAMPAIGN_MOVEMENT_RAW = 1_000;

    [Test, Description("No two catalogue rows share a code, because the code is what the upsert matches on and a duplicate would make one row overwrite the other.")]
    public void ShouldGiveEveryStructureADistinctCode()
    {
        Assert.That(StructureSeedData.Build().Select(structure => structure.Code), Is.Unique);
    }

    [Test, Description("No row takes negative work, which would make the construction process run backwards; zero is allowed and means a marker that needs no building.")]
    public void ShouldNeverAuthorNegativeBuildPoints()
    {
        foreach (UpsertStructureDto structure in StructureSeedData.Build())
        {
            Assert.That(structure.BuildPointsRequired, Is.GreaterThanOrEqualTo(0), $"{structure.Code} takes {structure.BuildPointsRequired} points of work.");
        }
    }

    /// <summary>
    /// The calibration the whole build system is anchored on, asserted as the arithmetic rather than as the numbers: 200 healthy humans on a full Turn finish a village in
    /// two Turns, and the 25 who are the smallest Force that may establish a camp finish one in a single Turn. Change a Power or a point total and this is what catches it.
    /// </summary>
    [Test, Description("A village takes two turns for 200 healthy humans on full movement, and a camp one turn for the 25 that are the smallest force allowed to establish one.")]
    public void ShouldCalibrateTheTwoSettlementTiersAgainstAHumanForce()
    {
        Dictionary<string, UpsertStructureDto> byCode = StructureSeedData.Build().ToDictionary(structure => structure.Code);

        long villageRate = ConstructionRateCalculator.CalculateBuildPointsPerTurn(
            _HUMAN_BODY_POWER_RAW,
            healthyCount: 200,
            campaignMovementRemainingRaw: _HUMAN_CAMPAIGN_MOVEMENT_RAW,
            campaignMovementPointsRaw: _HUMAN_CAMPAIGN_MOVEMENT_RAW);

        long campRate = ConstructionRateCalculator.CalculateBuildPointsPerTurn(
            _HUMAN_BODY_POWER_RAW,
            healthyCount: 25,
            campaignMovementRemainingRaw: _HUMAN_CAMPAIGN_MOVEMENT_RAW,
            campaignMovementPointsRaw: _HUMAN_CAMPAIGN_MOVEMENT_RAW);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                ConstructionRateCalculator.CalculateTurnsToComplete(byCode[StructureSeedData.VILLAGE_CODE].BuildPointsRequired, villageRate),
                Is.EqualTo(2),
                "200 healthy humans on a full turn should found a village in two turns.");

            Assert.That(
                ConstructionRateCalculator.CalculateTurnsToComplete(byCode[StructureSeedData.CAMP_CODE].BuildPointsRequired, campRate),
                Is.EqualTo(1),
                "The smallest force allowed a camp should establish one in a single turn.");
        }
    }

    [Test, Description("Halving the force, or spending half its movement, doubles how long a village takes — the lever the whole points model exists to give.")]
    [TestCase(100, 1_000, 4, Description = "Half the force, a full turn.")]
    [TestCase(200, 500, 4, Description = "The whole force, half a turn spent.")]
    [TestCase(100, 500, 8, Description = "Half the force and half a turn.")]
    [TestCase(400, 1_000, 1, Description = "Twice the force finishes within one turn.")]
    public void ShouldTakeLongerForASmallerOrMoreTravelledForce(int healthyCount, long movementRemainingRaw, int expectedTurns)
    {
        long villageBuildPoints = StructureSeedData.Build().Single(s => s.Code == StructureSeedData.VILLAGE_CODE).BuildPointsRequired;

        long rate = ConstructionRateCalculator.CalculateBuildPointsPerTurn(
            _HUMAN_BODY_POWER_RAW,
            healthyCount,
            movementRemainingRaw,
            _HUMAN_CAMPAIGN_MOVEMENT_RAW);

        Assert.That(ConstructionRateCalculator.CalculateTurnsToComplete(villageBuildPoints, rate), Is.EqualTo(expectedTurns));
    }

    [Test, Description("Both settlement tiers take the tile's Primary slot, which is what makes founding one displace whatever else held it.")]
    public void ShouldPutBothSettlementTiersInThePrimarySlot()
    {
        Dictionary<string, UpsertStructureDto> byCode = StructureSeedData.Build().ToDictionary(structure => structure.Code);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(byCode[StructureSeedData.VILLAGE_CODE].RequiresSlotType, Is.EqualTo(TileSlotType.Primary));
            Assert.That(byCode[StructureSeedData.CAMP_CODE].RequiresSlotType, Is.EqualTo(TileSlotType.Primary));
        }
    }

    [Test, Description("Every row can be built on the campaign map, since that is the only map a settle order or the construction step touches.")]
    public void ShouldMakeEveryStructureBuildableOnTheCampaignMap()
    {
        foreach (UpsertStructureDto structure in StructureSeedData.Build())
        {
            Assert.That(structure.CanBeBuiltOnCampaignMap, Is.True, $"{structure.Code} cannot be built on the campaign map.");
        }
    }

    [Test, Description("Every row names map artwork, because a kind with none would be in the catalogue and absent from the map with nothing to say so.")]
    public void ShouldGiveEveryStructureAMapImage()
    {
        foreach (UpsertStructureDto structure in StructureSeedData.Build())
        {
            Assert.That(structure.MapImagePath, Is.Not.Empty, $"{structure.Code} names no map image.");
        }
    }

    [Test, Description("Every row sees at least its own tile, since a Structure is an observer and one with no sight would blind the ground it stands on.")]
    public void ShouldGiveEveryStructureAtLeastItsOwnTileOfSight()
    {
        foreach (UpsertStructureDto structure in StructureSeedData.Build())
        {
            Assert.That(structure.SightRange, Is.GreaterThanOrEqualTo(1), $"{structure.Code} has a sight range of {structure.SightRange}.");
        }
    }
}
