using AscendantEmpire.Application.Features.ForceOrders.Movement;
using AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.ForceOrders.Movement;

/// <summary>
/// Pure unit tests for the movement domain strategies — no database, because each reads only the tile snapshot it is handed.
/// </summary>
[TestFixture]
internal class MovementDomainStrategyTests
{
    /// <summary>Every registered strategy, in the order they are wired up in <c>ApplicationStartup</c>.</summary>
    private static IReadOnlyCollection<IMovementDomainStrategy> AllStrategies =>
    [
        new LandMovementStrategy(),
        new WaterMovementStrategy(),
        new BelowWaterMovementStrategy(),
        new AirMovementStrategy(),
        new NearEarthOrbitMovementStrategy()
    ];

    private static MovementTargetTile LandTile()
    {
        return new MovementTargetTile
        {
            XCoord = 3,
            YCoord = 3,
            GeographyType = CampaignMapGeographyType.Flat,
            GeographyName = "Flat",
            GeographyMovementCost = 1m,
            BiomeName = "Grassland",
            BiomeMovementCost = 1m,
            BiomeIsWater = false,
            BiomeIsTraversable = true
        };
    }

    private static MovementTargetTile WaterTile()
    {
        return new MovementTargetTile
        {
            XCoord = 3,
            YCoord = 4,
            GeographyType = CampaignMapGeographyType.DeepWater,
            GeographyName = "Deep Water",
            GeographyMovementCost = 1m,
            BiomeName = "Ocean",
            BiomeMovementCost = 1m,
            BiomeIsWater = true,
            BiomeIsTraversable = true
        };
    }

    [Test, Description("A domain with no strategy would have no rule to be judged against and every one of its moves refused, so all five must be covered.")]
    public void ShouldProvideAStrategyForEveryMovementDomain()
    {
        IEnumerable<DomainType> covered = AllStrategies.Select(strategy => strategy.MovementDomain);

        Assert.That(covered, Is.EquivalentTo(Enum.GetValues<DomainType>()));
    }

    [Test, Description("The step handler resolves a strategy by its domain with SingleOrDefault, so two claiming the same domain would throw at move time.")]
    public void ShouldNotRegisterTwoStrategiesForTheSameDomain()
    {
        IEnumerable<DomainType> covered = AllStrategies.Select(strategy => strategy.MovementDomain);

        Assert.That(covered.Distinct().Count(), Is.EqualTo(AllStrategies.Count));
    }

    [Test, Description("Land forces cross dry ground; nothing yet carries them over water, so water is refused rather than made expensive.")]
    public void ShouldLetALandForceOntoLandButNotWater()
    {
        var strategy = new LandMovementStrategy();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(strategy.GetEntryRefusal(LandTile()), Is.Null);
            Assert.That(strategy.GetEntryRefusal(WaterTile()), Is.EqualTo("Cannot enter water."));
        }
    }

    [Test, Description("The mirror of the land rule: beaching a ship is a landing, not a move, so a water force is refused dry land.")]
    public void ShouldLetAWaterForceOntoWaterButNotLand()
    {
        var strategy = new WaterMovementStrategy();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(strategy.GetEntryRefusal(WaterTile()), Is.Null);
            Assert.That(strategy.GetEntryRefusal(LandTile()), Is.EqualTo("Cannot enter land."));
        }
    }

    [Test, Description("BelowWater follows the surface water rule for now; without its own strategy every submarine move would be refused outright.")]
    public void ShouldLetASubmergedForceOntoWaterButNotLand()
    {
        var strategy = new BelowWaterMovementStrategy();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(strategy.GetEntryRefusal(WaterTile()), Is.Null);
            Assert.That(strategy.GetEntryRefusal(LandTile()), Is.EqualTo("Cannot enter land."));
        }
    }

    [Test, Description("Flight lifts the land and water restriction only; impassability is settled by the handler before any strategy is consulted.")]
    public void ShouldLetAnAirForceOverAnyTerrain()
    {
        var strategy = new AirMovementStrategy();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(strategy.GetEntryRefusal(LandTile()), Is.Null);
            Assert.That(strategy.GetEntryRefusal(WaterTile()), Is.Null);
        }
    }

    /// <summary>A step onto the given tile with the given rivers, from dry land, straight on.</summary>
    private static MovementStep Step(MovementTargetTile target, RiverWidth crossed = RiverWidth.None, RiverWidth along = RiverWidth.None)
    {
        return new MovementStep
        {
            Target = target,
            OriginIsWater = false,
            IsDiagonal = false,
            Rivers = new RiverStepEffect(crossed, along, RiverWidth.None)
        };
    }

    [Test, Description("A Land Force can cross a Medium river but nothing carries it over a Large one until bridges or fords exist.")]
    public void ShouldRefuseALandForceALargeRiverCrossing()
    {
        var strategy = new LandMovementStrategy();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(strategy.GetStepRefusal(Step(LandTile(), crossed: RiverWidth.Medium)), Is.Null);
            Assert.That(strategy.GetStepRefusal(Step(LandTile(), crossed: RiverWidth.Large)), Is.EqualTo("Cannot cross a large river."));
            Assert.That(strategy.GetStepRefusal(Step(WaterTile())), Is.EqualTo("Cannot enter water."), "The tile rule still comes first.");
        }
    }

    [Test, Description("Land Forces are the ones rivers slow and speed, so their step cost is the tile's cost adjusted for the rivers.")]
    public void ShouldPriceALandStepWithItsRivers()
    {
        var strategy = new LandMovementStrategy();

        Assert.That(strategy.GetStepCost(Step(LandTile(), crossed: RiverWidth.Medium)), Is.EqualTo(1m + RiverMovementRules.MEDIUM_RIVER_CROSSING_COST));
    }

    [Test, Description("Rivers are no obstacle to flight or orbit: a Large crossing is neither refused nor charged, and a run along one is no cheaper.")]
    public void ShouldLetAirAndOrbitIgnoreRivers()
    {
        IMovementDomainStrategy[] strategies = [new AirMovementStrategy(), new NearEarthOrbitMovementStrategy()];

        using (Assert.EnterMultipleScope())
        {
            foreach (IMovementDomainStrategy strategy in strategies)
            {
                Assert.That(strategy.GetStepRefusal(Step(LandTile(), crossed: RiverWidth.Large)), Is.Null, $"{strategy.MovementDomain} should cross a Large river.");
                Assert.That(strategy.GetStepCost(Step(LandTile(), crossed: RiverWidth.Medium, along: RiverWidth.Large)), Is.EqualTo(1m), $"{strategy.MovementDomain} should pay only the tile.");
            }
        }
    }

    [Test, Description("Surface and submerged ships both sail up Large rivers, so both follow the sailing rules rather than the plain water rule on a step.")]
    public void ShouldLetBothShipDomainsSailUpALargeRiver()
    {
        IMovementDomainStrategy[] strategies = [new WaterMovementStrategy(), new BelowWaterMovementStrategy()];

        using (Assert.EnterMultipleScope())
        {
            foreach (IMovementDomainStrategy strategy in strategies)
            {
                Assert.That(strategy.GetStepRefusal(Step(LandTile(), along: RiverWidth.Large)), Is.Null, $"{strategy.MovementDomain} should sail up the river.");
                Assert.That(strategy.GetEntryRefusal(LandTile()), Is.EqualTo("Cannot enter land."), "As a tile question, which fog of war asks, land is still refused.");
            }
        }
    }

    [Test, Description("Orbit passes above the map entirely, so it is unrestricted for the same reason flight is.")]
    public void ShouldLetAnOrbitalForceOverAnyTerrain()
    {
        var strategy = new NearEarthOrbitMovementStrategy();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(strategy.GetEntryRefusal(LandTile()), Is.Null);
            Assert.That(strategy.GetEntryRefusal(WaterTile()), Is.Null);
        }
    }
}
