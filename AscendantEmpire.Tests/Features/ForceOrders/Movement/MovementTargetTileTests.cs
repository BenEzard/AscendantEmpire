using AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.ForceOrders.Movement;

/// <summary>
/// Pure unit tests for the movement arithmetic on <see cref="MovementTargetTile"/> — no database, because it is derived from a flat snapshot.
/// </summary>
/// <remarks>
/// The costs used here are the ones in <c>GeographySeedData</c> and <c>BiomeSeedData</c>, so a change to the seeded catalogue that would silently
/// alter how far a Force can travel shows up as a failure here.
/// </remarks>
[TestFixture]
internal class MovementTargetTileTests
{
    /// <summary>Builds a target tile, defaulting to ordinary Flat grassland at cost 1.</summary>
    private static MovementTargetTile Tile(
        CampaignMapGeographyType geographyType = CampaignMapGeographyType.Flat,
        decimal geographyMovementCost = 1m,
        decimal biomeMovementCost = 1m,
        bool biomeIsWater = false,
        bool biomeIsTraversable = true)
    {
        return new MovementTargetTile
        {
            XCoord = 4,
            YCoord = 7,
            GeographyType = geographyType,
            GeographyName = geographyType.ToString(),
            GeographyMovementCost = geographyMovementCost,
            BiomeName = "Grassland",
            BiomeMovementCost = biomeMovementCost,
            BiomeIsWater = biomeIsWater,
            BiomeIsTraversable = biomeIsTraversable
        };
    }

    [Test, Description("The biome is a modifier on the ground, so it adds only its excess over 1 and a neutral biome leaves the geography's cost alone.")]
    [TestCase(1.0, 1.0, 1.0, Description = "Flat under Grassland: the ordinary case, and the one everything else is measured against.")]
    [TestCase(1.0, 1.1, 1.1, Description = "Flat under Desert: the biome's tenth is added rather than multiplied away.")]
    [TestCase(2.0, 1.0, 2.0, Description = "Hilly under Forest: a neutral biome must not double the hill.")]
    [TestCase(2.0, 1.1, 2.1, Description = "Hilly under Desert: both contributions land together.")]
    public void ShouldCombineGeographyAndBiomeCost(decimal geographyCost, decimal biomeCost, decimal expectedCost)
    {
        MovementTargetTile tile = Tile(geographyMovementCost: geographyCost, biomeMovementCost: biomeCost);

        Assert.That(tile.MovementCost, Is.EqualTo(expectedCost));
    }

    [Test, Description("Mountainous marks itself impassable through the geography cost sentinel rather than by any flag, so the sentinel has to be honoured.")]
    public void ShouldTreatTheImpassableCostSentinelAsImpassable()
    {
        MovementTargetTile tile = Tile(
            geographyType: CampaignMapGeographyType.Mountainous,
            geographyMovementCost: CampaignGeography.IMPASSABLE_MOVEMENT_COST);

        Assert.That(tile.IsImpassable, Is.True);
    }

    [Test, Description("The Void biome marks itself impassable with a flag instead, so both markers have to be checked and not just the cost one.")]
    public void ShouldTreatANonTraversableBiomeAsImpassable()
    {
        MovementTargetTile tile = Tile(biomeMovementCost: 99m, biomeIsTraversable: false);

        Assert.That(tile.IsImpassable, Is.True);
    }

    [Test, Description("A refusal names the mountain only when the ground is the blocker, so an impassable biome must not be mistaken for one.")]
    public void ShouldTellImpassableGroundApartFromAnImpassableBiome()
    {
        MovementTargetTile mountain = Tile(
            geographyType: CampaignMapGeographyType.Mountainous,
            geographyMovementCost: CampaignGeography.IMPASSABLE_MOVEMENT_COST);

        MovementTargetTile voidTile = Tile(biomeMovementCost: 99m, biomeIsTraversable: false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(mountain.GeographyIsImpassable, Is.True);
            Assert.That(voidTile.GeographyIsImpassable, Is.False, "The Void is impassable through its biome, and has no mountain to name.");
            Assert.That(Tile().GeographyIsImpassable, Is.False);
        }
    }

    [Test, Description("Ordinary ground must not be caught by either impassability marker, or nothing could move anywhere.")]
    public void ShouldTreatOrdinaryGroundAsPassable()
    {
        Assert.That(Tile().IsImpassable, Is.False);
    }

    [Test, Description("Either marker alone means water, so a hand-edited map that sets only one of them still stops a land force walking out to sea.")]
    [TestCase(CampaignMapGeographyType.DeepWater, false, true, Description = "Water geography with a biome that does not say so.")]
    [TestCase(CampaignMapGeographyType.ShallowWater, false, true, Description = "Shallow water counts as water too.")]
    [TestCase(CampaignMapGeographyType.Flat, true, true, Description = "Water biome over a geography that does not say so.")]
    [TestCase(CampaignMapGeographyType.Flat, false, false, Description = "Neither marker set is dry land.")]
    public void ShouldIdentifyWaterFromEitherMarker(CampaignMapGeographyType geographyType, bool biomeIsWater, bool expectedIsWater)
    {
        MovementTargetTile tile = Tile(geographyType: geographyType, biomeIsWater: biomeIsWater);

        Assert.That(tile.IsWater, Is.EqualTo(expectedIsWater));
    }

    [Test, Description("Walks the scenario table in unit-mechanics.md: half the cost lets a force in, the whole cost is what it pays, and the balance may go negative.")]
    [TestCase(1.0, 1.0, true, 0.0, Description = "Warrior onto Plain: enters on 0.5 required, ends on 0.")]
    [TestCase(1.0, 2.0, true, -1.0, Description = "Warrior onto Plain+Forest: enters on 1.0 required and finishes in debt at -1.")]
    [TestCase(2.0, 1.0, true, 1.0, Description = "Cavalry onto Plain: enters on 0.5 required and has 1 left to move again.")]
    [TestCase(2.0, 2.0, true, 0.0, Description = "Cavalry onto Forest: enters on 1.0 required, ends on 0.")]
    [TestCase(0.0, 1.0, false, 0.0, Description = "A spent force cannot afford even half of the cheapest tile.")]
    [TestCase(0.4, 1.0, false, 0.4, Description = "Just under half the cost is still not enough to enter.")]
    [TestCase(0.5, 1.0, true, -0.5, Description = "Exactly half the cost is enough, and paying the whole cost takes it negative.")]
    public void ShouldFollowTheDesignDocumentEntryAndPaymentRule(
        decimal movementPointsBefore,
        decimal tileCost,
        bool expectedCanEnter,
        decimal expectedMovementPointsAfter)
    {
        MovementTargetTile tile = Tile(geographyMovementCost: tileCost);

        // The rule as the handler applies it: half the cost is the threshold to enter, the whole cost is the price once there.
        decimal requiredToEnter = tile.MovementCost / 2m;
        bool canEnter = movementPointsBefore >= requiredToEnter;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(canEnter, Is.EqualTo(expectedCanEnter));

            decimal movementPointsAfter = canEnter ? movementPointsBefore - tile.MovementCost : movementPointsBefore;
            Assert.That(movementPointsAfter, Is.EqualTo(expectedMovementPointsAfter));
        }
    }

    [Test, Description("The refusal message names the tile the player was aiming at, so a rejected step is traceable to a square on the map.")]
    public void ShouldDescribeItselfByTerrainAndCoordinate()
    {
        MovementTargetTile tile = Tile(geographyType: CampaignMapGeographyType.Hilly);

        Assert.That(tile.Describe(), Is.EqualTo("Hilly Grassland at (4, 7)"));
    }
}
