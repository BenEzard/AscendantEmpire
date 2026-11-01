using AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Maps.CampaignMaps;

/// <summary>
/// Pure unit tests for <see cref="BlockedSightReveal"/> — no database, because the rule reads only the tile snapshots it is handed.
/// </summary>
/// <remarks>
/// <para>
/// These pin the one thing an Observer blinded by dense terrain can still report: the adjacent Tiles its movement domain is barred from, and never
/// the ones it could have walked onto to look. Getting that backwards either makes a Jungle worthless as cover or leaves a Jungle coastline unable
/// to discover the sea it sits on.
/// </para>
/// <para>
/// The strategies are the real ones rather than substitutes, because the whole point of the rule is that it agrees with what a step would do.
/// </para>
/// </remarks>
[TestFixture]
internal class BlockedSightRevealTests
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
            GeographyIsWater = false,
            BiomeIsTraversable = true,
            GeographyIsTraversable = true
        };
    }

    private static MovementTargetTile WaterTile()
    {
        return new MovementTargetTile
        {
            XCoord = 4,
            YCoord = 3,
            GeographyType = CampaignMapGeographyType.DeepWater,
            GeographyName = "Deep Water",
            GeographyMovementCost = 1m,
            BiomeName = "Ocean",
            BiomeMovementCost = 1m,
            BiomeIsWater = true,
            GeographyIsWater = true,
            BiomeIsTraversable = true,
            GeographyIsTraversable = true
        };
    }

    /// <summary>Mountainous ground, which the seed data marks untraversable. Its cost carries the same fact, and nothing reads it to answer the question.</summary>
    private static MovementTargetTile MountainTile()
    {
        return new MovementTargetTile
        {
            XCoord = 3,
            YCoord = 2,
            GeographyType = CampaignMapGeographyType.Mountainous,
            GeographyName = "Mountainous",
            GeographyMovementCost = CampaignGeography.IMPASSABLE_MOVEMENT_COST,
            BiomeName = "Arid",
            BiomeMovementCost = 1m,
            BiomeIsWater = false,
            GeographyIsWater = false,
            BiomeIsTraversable = true,
            GeographyIsTraversable = false
        };
    }

    /// <summary>
    /// The Void, which is impassable by the other of the two markers: a Biome that is simply not traversable.
    /// </summary>
    /// <remarks>
    /// The Geography stays Flat, because the Void exists only as a Biome — <c>CampaignMapGeographyType</c> has no member for it. That is exactly why
    /// <c>MovementTargetTile.IsImpassable</c> tests both markers.
    /// </remarks>
    private static MovementTargetTile VoidTile()
    {
        return new MovementTargetTile
        {
            XCoord = 2,
            YCoord = 3,
            GeographyType = CampaignMapGeographyType.Flat,
            GeographyName = "Flat",
            GeographyMovementCost = 1m,
            BiomeName = "Void",
            BiomeMovementCost = 1m,
            BiomeIsWater = false,
            GeographyIsWater = false,
            BiomeIsTraversable = false,
            GeographyIsTraversable = true
        };
    }

    private static IReadOnlyCollection<MovementTargetTile> Reveal(DomainType movementDomain, params MovementTargetTile[] adjacentTiles)
    {
        return BlockedSightReveal.SelectRevealedTiles(AllStrategies, movementDomain, adjacentTiles);
    }

    [Test, Description("The reported bug: a land Force blinded in a coastal Jungle uncovers the sea beside it, which it could never step onto to discover.")]
    public void ShouldRevealWaterToABlindedLandObserver()
    {
        MovementTargetTile water = WaterTile();

        Assert.That(Reveal(DomainType.Land, water), Is.EquivalentTo([water]));
    }

    [Test, Description("The mechanic being protected: dry ground beside a blinded land Force stays dark, because it can walk out of the Jungle and look back.")]
    public void ShouldNotRevealLandToABlindedLandObserver()
    {
        Assert.That(Reveal(DomainType.Land, LandTile()), Is.Empty, "A Force deep in a Jungle must uncover nothing at all, or the Jungle stops being worth hiding in.");
    }

    [Test, Description("Only the barred neighbours are handed back, so a Jungle shoreline uncovers the sea without also uncovering the coast beside it.")]
    public void ShouldRevealOnlyTheBarredNeighboursOfAMixedRing()
    {
        MovementTargetTile water = WaterTile();
        MovementTargetTile mountain = MountainTile();

        IReadOnlyCollection<MovementTargetTile> revealed = Reveal(DomainType.Land, LandTile(), water, mountain);

        Assert.That(revealed, Is.EquivalentTo([water, mountain]));
    }

    [Test, Description("Impassability holds for every domain alike, so ground nothing can enter is uncovered whoever is standing next to it.")]
    [TestCase(DomainType.Land, Description = "A Force on foot.")]
    [TestCase(DomainType.Water, Description = "A ship.")]
    [TestCase(DomainType.Air, Description = "A flier, which is refused nothing else at all.")]
    [TestCase(DomainType.NearEarthOrbit, Description = "The same, from orbit.")]
    public void ShouldRevealImpassableGroundToEveryDomain(DomainType movementDomain)
    {
        MovementTargetTile mountain = MountainTile();
        MovementTargetTile emptiness = VoidTile();

        Assert.That(Reveal(movementDomain, mountain, emptiness), Is.EquivalentTo([mountain, emptiness]));
    }

    [Test, Description("The naval mirror: a ship blinded in a Mangrove uncovers the shore it cannot beach on, for the same reason a Force uncovers the sea.")]
    public void ShouldRevealLandToABlindedWaterObserver()
    {
        MovementTargetTile land = LandTile();

        IReadOnlyCollection<MovementTargetTile> revealed = Reveal(DomainType.Water, land, WaterTile());

        Assert.That(revealed, Is.EquivalentTo([land]), "Open water is the one thing a ship could have sailed to, so it is the one thing it does not get for free.");
    }

    [Test, Description("A flier is barred from nothing passable, so being blinded in a Jungle uncovers no water for it however much surrounds it.")]
    public void ShouldRevealNothingPassableToABlindedAirObserver()
    {
        Assert.That(Reveal(DomainType.Air, LandTile(), WaterTile()), Is.Empty);
    }

    [Test, Description("A blinded Observer with no barred neighbours reveals nothing, which is the ordinary case in the middle of a forest.")]
    public void ShouldRevealNothingWhenEveryNeighbourCanBeEntered()
    {
        Assert.That(Reveal(DomainType.Land, LandTile(), LandTile(), LandTile()), Is.Empty);
    }

    [Test, Description("An unregistered domain strategy reveals no passable ground, because vision has to fail closed where a step fails shut.")]
    public void ShouldRevealNoPassableGroundWhenNoStrategyIsRegisteredForTheDomain()
    {
        MovementTargetTile mountain = MountainTile();

        IReadOnlyCollection<MovementTargetTile> revealed = BlockedSightReveal.SelectRevealedTiles(
            movementDomainStrategies: [],
            DomainType.Land,
            [LandTile(), WaterTile(), mountain]);

        // The mountain survives on purpose: impassability is settled before any strategy is consulted, so it needs none to be judged. Water is the
        // interesting one — it is refused only by a strategy, and with none registered that refusal is a wiring fault rather than a fact about the
        // terrain. Reading it as "could not walk there" would hand the Player the ring around every blinded Observer they own.
        Assert.That(revealed, Is.EquivalentTo([mountain]));
    }
}
