using AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.ForceOrders.Movement;

/// <summary>
/// Pure unit tests for the movement domain strategies — no database, because each reads only the tile snapshot it is handed.
/// </summary>
[TestFixture]
internal class MovementDomainStrategyTests
{
    /// <summary>Every registered strategy, in the order they are wired up in <c>MediatrStartup</c>.</summary>
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

    [Test, Description("A refusal is shown to the player as it stands, so it has to name the tile rather than leave them guessing which square was rejected.")]
    public void ShouldNameTheTileInARefusal()
    {
        string? refusal = new LandMovementStrategy().GetEntryRefusal(WaterTile());

        Assert.That(refusal, Does.Contain("Deep Water Ocean at (3, 4)"));
    }
}
