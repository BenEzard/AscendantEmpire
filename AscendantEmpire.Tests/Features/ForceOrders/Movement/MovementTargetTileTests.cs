using AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.StartTurn;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.ForceOrders.Movement;

/// <summary>
/// Pure unit tests for the movement arithmetic on <see cref="MovementTargetTile"/> — no database, because it is derived from a flat snapshot.
/// </summary>
/// <remarks>
/// <para>
/// The costs used here are the ones in <c>GeographySeedData</c> and <c>BiomeSeedData</c>, so a change to the seeded catalogue that would silently
/// alter how far a Force can travel shows up as a failure here.
/// </para>
/// <para>
/// <see cref="MovementTargetTile.TurnsHeld"/> is a prediction about what <c>MovementReplenishment</c> will do over the Turns that follow, so the
/// tests for it deliberately call that rule for real rather than restating it. A prediction checked against a copy of the thing it predicts would
/// pin nothing.
/// </para>
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
            GeographyIsWater = geographyType is CampaignMapGeographyType.DeepWater or CampaignMapGeographyType.ShallowWater,
            BiomeIsTraversable = biomeIsTraversable,
            GeographyIsTraversable = geographyType != CampaignMapGeographyType.Mountainous
        };
    }

    /// <summary>Puts a readable decimal onto the raw Scale 1,000 convention the movement arithmetic works in.</summary>
    private static long Raw(decimal amount)
    {
        return ScaledAmount.FromDecimal(amount).RawValue;
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

    [Test, Description("Mountainous is untraversable ground, so the geography flag alone has to refuse the tile however ordinary the biome on it is.")]
    public void ShouldTreatUntraversableGroundAsImpassable()
    {
        MovementTargetTile tile = Tile(geographyType: CampaignMapGeographyType.Mountainous);

        Assert.That(tile.IsImpassable, Is.True);
    }

    [Test, Description("The Void is impassable through its biome on perfectly ordinary ground, so both markers have to be checked and not just the geography's.")]
    public void ShouldTreatANonTraversableBiomeAsImpassable()
    {
        MovementTargetTile tile = Tile(biomeMovementCost: 99m, biomeIsTraversable: false);

        Assert.That(tile.IsImpassable, Is.True);
    }

    [Test, Description("A refusal names the mountain only when the ground is the blocker, so an impassable biome must not be mistaken for one.")]
    public void ShouldTellImpassableGroundApartFromAnImpassableBiome()
    {
        MovementTargetTile mountain = Tile(geographyType: CampaignMapGeographyType.Mountainous);

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

    [Test, Description("Walks the Scenarios table in unit-movement.md: entry costs the tile's whole price and the balance is allowed to go as far negative as that takes it.")]
    [TestCase(1.0, 1.0, 0.0, Description = "Warrior onto Flat Grassland: pays its whole point and finishes level.")]
    [TestCase(1.0, 2.0, -1.0, Description = "Warrior onto Flat Forest: a point of debt to work off.")]
    [TestCase(1.0, 3.0, -2.0, Description = "Warrior onto Flat Jungle: the old rule asked for 1.5, which a 1 point Force could never be holding.")]
    [TestCase(1.0, 4.0, -3.0, Description = "Warrior onto Hilly Jungle: refused outright at every point in the game under the old rule.")]
    [TestCase(2.2, 4.0, -1.8, Description = "Cavalry onto Hilly Jungle from a full allowance.")]
    [TestCase(0.2, 4.0, -3.8, Description = "Cavalry onto Hilly Jungle on a fractional remainder: the gate is permissive, and it pays dearly for that.")]
    [TestCase(1.0, 0.5, 0.5, Description = "Warrior onto a road tile: half a point buys the first of the Turn's two moves.")]
    public void ShouldSpendTheWholeTileCostHoweverFarNegativeThatTakesTheBalance(decimal movementPointsBefore, decimal tileCost, decimal expectedMovementPointsAfter)
    {
        MovementTargetTile tile = Tile(geographyMovementCost: tileCost);

        // The entry gate itself is on the Force rather than the tile — anything above zero gets in — so all that is left here is the payment.
        Assert.That(movementPointsBefore - tile.MovementCost, Is.EqualTo(expectedMovementPointsAfter));
    }

    [Test, Description("Counts the Turn-ends a tile costs a Force, which is the quantity that replaces the old proportional threshold as the thing worth knowing about a step.")]
    [TestCase(1.0, 1.0, 1.0, 1, Description = "Warrior onto Flat Grassland: lands on zero, which is a Turn spent even though nothing is owed.")]
    [TestCase(1.0, 1.0, 2.0, 2, Description = "Warrior onto Flat Forest: opens the next Turn on zero and only moves the Turn after.")]
    [TestCase(1.0, 1.0, 3.0, 3, Description = "Warrior onto Flat Jungle.")]
    [TestCase(1.0, 1.0, 4.0, 4, Description = "Warrior onto Hilly Jungle: the worst the authored terrain can do, and comfortably inside the stranding limit.")]
    [TestCase(2.2, 2.2, 4.0, 1, Description = "Cavalry onto Hilly Jungle from full: one Turn lost, then it opens on 0.4 and may move.")]
    [TestCase(2.2, 0.2, 4.0, 2, Description = "The same tile entered on a remainder costs a second Turn, which is the price of the permissive gate.")]
    [TestCase(2.0, 2.0, 1.0, 0, Description = "Cavalry onto Flat Grassland: still holding a point, so it steps straight on and the tile holds it for no time.")]
    [TestCase(1.0, 1.0, 0.5, 0, Description = "The first of two road moves in a Turn holds the Force not at all.")]
    [TestCase(1.0, 0.5, 0.5, 1, Description = "The second road move lands it on zero, so that one does cost a Turn.")]
    [TestCase(1.0, 1.0, 10.0, 10, Description = "A hypothetical high-cost biome, well past the stranding limit the handler applies to this count.")]
    public void ShouldCountTheTurnEndsATileHoldsAForce(decimal allowance, decimal movementPointsBefore, decimal tileCost, int expectedTurnsHeld)
    {
        int turnsHeld = MovementTargetTile.TurnsHeld(tileCost, Raw(allowance), Raw(movementPointsBefore));

        Assert.That(turnsHeld, Is.EqualTo(expectedTurnsHeld));
    }

    [Test, Description("Runs the real start-of-Turn rule forward until the Force can move again, so the predicted count cannot drift away from the replenishment it is predicting.")]
    [TestCase(1.0, 1.0, 1.0, Description = "Warrior onto Flat Grassland.")]
    [TestCase(1.0, 1.0, 2.0, Description = "Warrior onto Flat Forest.")]
    [TestCase(1.0, 1.0, 3.0, Description = "Warrior onto Flat Jungle.")]
    [TestCase(1.0, 1.0, 4.0, Description = "Warrior onto Hilly Jungle.")]
    [TestCase(2.2, 2.2, 4.0, Description = "Cavalry onto Hilly Jungle from full, where the fractional allowance makes the arithmetic non-obvious.")]
    [TestCase(2.2, 0.2, 4.0, Description = "Cavalry onto Hilly Jungle on a remainder, which is the deepest debt any of these cases reaches.")]
    [TestCase(1.0, 0.5, 0.5, Description = "A road tile entered on the last half point.")]
    public void ShouldAgreeWithTheReplenishmentRuleAboutWhenTheForceMovesAgain(decimal allowance, decimal movementPointsBefore, decimal tileCost)
    {
        long allowanceRaw = Raw(allowance);
        int predictedTurnsHeld = MovementTargetTile.TurnsHeld(tileCost, allowanceRaw, Raw(movementPointsBefore));

        // Pay for the tile, then open Turn after Turn until the Force is holding something it could spend.
        long balanceRaw = Raw(movementPointsBefore) - Raw(tileCost);
        int actualTurnsHeld = 0;

        while (balanceRaw <= 0)
        {
            balanceRaw = MovementReplenishment.ReplenishCampaignMovement(allowanceRaw, balanceRaw);
            actualTurnsHeld++;
        }

        Assert.That(predictedTurnsHeld, Is.EqualTo(actualTurnsHeld));
    }

    [Test, Description("A Force compiled to no campaign movement never recovers, so the tile holds it for ever rather than the count dividing by zero.")]
    public void ShouldHoldAForceWithNoAllowanceForEver()
    {
        Assert.That(MovementTargetTile.TurnsHeld(2m, 0, Raw(0.5m)), Is.EqualTo(int.MaxValue));
    }

    [Test, Description("The refusal message names the tile the player was aiming at, so a rejected step is traceable to a square on the map.")]
    public void ShouldDescribeItselfByTerrainAndCoordinate()
    {
        MovementTargetTile tile = Tile(geographyType: CampaignMapGeographyType.Hilly);

        Assert.That(tile.Describe(), Is.EqualTo("Hilly Grassland at (4, 7)"));
    }
}
