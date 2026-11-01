using AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Maps.CampaignMaps;

/// <summary>
/// A whole campaign map of <see cref="SightTerrain"/>, Flat Grassland unless a test paints or raises a tile, for exercising the sight rules without a database.
/// </summary>
/// <remarks>
/// The terrain facts mirror the seed data — Jungle blocks sight, Ocean is water, the Void blocks sight and cannot be entered, Hilly stands at 1 and
/// Mountainous at 3 with the impassable movement cost — so a rule that passes against this grid is describing terrain that exists.
/// </remarks>
internal sealed class SightTerrainGrid
{
    private readonly Dictionary<(int X, int Y), SightTerrain> _terrain = [];

    internal SightTerrainGrid(CampaignMapGeometry geometry)
    {
        Geometry = geometry;

        for (int y = 0; y < geometry.Height; y++)
        {
            for (int x = 0; x < geometry.Width; x++)
            {
                _terrain[(x, y)] = BuildTile(x, y, BiomeType.Grassland, CampaignMapGeographyType.Flat);
            }
        }
    }

    internal CampaignMapGeometry Geometry { get; }

    internal IReadOnlyDictionary<(int X, int Y), SightTerrain> Terrain => _terrain;

    internal SightTerrain this[int x, int y] => _terrain[(x, y)];

    /// <summary>Repaints one tile's Biome, keeping its Geography.</summary>
    internal SightTerrainGrid Paint(int x, int y, BiomeType biomeType)
    {
        _terrain[(x, y)] = BuildTile(x, y, biomeType, _terrain[(x, y)].GeographyType);
        return this;
    }

    /// <summary>Changes one tile's Geography, keeping its Biome.</summary>
    internal SightTerrainGrid Raise(int x, int y, CampaignMapGeographyType geographyType)
    {
        _terrain[(x, y)] = BuildTile(x, y, _terrain[(x, y)].BiomeType, geographyType);
        return this;
    }

    /// <summary>The world-map id of a tile, from the grid's own geometry.</summary>
    internal int IdOf(int x, int y)
    {
        return Geometry.GetCampaignMapId(new Coord(x, y));
    }

    private SightTerrain BuildTile(int x, int y, BiomeType biomeType, CampaignMapGeographyType geographyType)
    {
        (bool doesBlockSight, bool isWater, bool isTraversable) = biomeType switch
        {
            BiomeType.Jungle => (true, false, true),
            BiomeType.Ocean => (false, true, true),
            BiomeType.Void => (true, false, false),
            _ => (false, false, true)
        };

        (int relativeElevation, decimal geographyMovementCost) = geographyType switch
        {
            CampaignMapGeographyType.Hilly => (1, 2m),
            CampaignMapGeographyType.Mountainous => (3, CampaignGeography.IMPASSABLE_MOVEMENT_COST),
            _ => (0, 1m)
        };

        return new SightTerrain
        {
            CampaignMapId = Geometry.GetCampaignMapId(new Coord(x, y)),
            X = x,
            Y = y,
            BiomeType = biomeType,
            GeographyType = geographyType,
            DoesBlockSight = doesBlockSight,
            RelativeElevation = relativeElevation,
            GeographyName = geographyType.ToString(),
            GeographyMovementCost = geographyMovementCost,
            BiomeName = biomeType.ToString(),
            BiomeMovementCost = 1m,
            BiomeIsWater = isWater,
            // Derived from the geography, as the seeded catalogue has it: only Deep and Shallow Water are water ground.
            GeographyIsWater = geographyType is CampaignMapGeographyType.DeepWater or CampaignMapGeographyType.ShallowWater,
            BiomeIsTraversable = isTraversable,
            GeographyIsTraversable = geographyType != CampaignMapGeographyType.Mountainous
        };
    }
}

/// <summary>
/// Builders shared by the sight fixtures.
/// </summary>
internal static class SightTestData
{
    /// <summary>Every registered movement domain strategy, as wired up in <c>ApplicationStartup</c>.</summary>
    internal static IReadOnlyCollection<IMovementDomainStrategy> AllStrategies =>
    [
        new LandMovementStrategy(),
        new WaterMovementStrategy(),
        new BelowWaterMovementStrategy(),
        new AirMovementStrategy(),
        new NearEarthOrbitMovementStrategy()
    ];

    /// <summary>A land Unit Observer with a middling Detection, still standing unless told otherwise.</summary>
    internal static SightObserver Unit(int id, int playerId, int x, int y, int sightRange, int? destroyedAtSequence = null)
    {
        return new SightObserver
        {
            Id = id,
            RecordType = RecordType.Unit,
            PlayerId = playerId,
            Location = new Coord(x, y),
            BaseSightRange = sightRange,
            MovementDomain = DomainType.Land,
            Detection = 1000,
            HealthyTroops = 50,
            UnitTemplateId = 1,
            DestroyedAtSequence = destroyedAtSequence
        };
    }

    /// <summary>A Structure Observer with the stand-in Structure Detection, still standing unless told the sequence it was lost at.</summary>
    internal static SightObserver Structure(int id, int playerId, int x, int y, int sightRange, int? destroyedAtSequence = null)
    {
        return new SightObserver
        {
            Id = id,
            RecordType = RecordType.Structure,
            PlayerId = playerId,
            Location = new Coord(x, y),
            BaseSightRange = sightRange,
            MovementDomain = DomainType.Land,
            Detection = SightingFidelityRules.STRUCTURE_DETECTION,
            DestroyedAtSequence = destroyedAtSequence
        };
    }

    /// <summary>The fields of view of the given Observers over the grid, computed by the real rules.</summary>
    internal static IReadOnlyCollection<ObserverFieldOfView> ViewsOf(SightTerrainGrid grid, params SightObserver[] observers)
    {
        return
        [
            .. observers.Select(observer => new ObserverFieldOfView(
                observer,
                FieldOfView.ComputeSeenTiles(observer, grid.Terrain, grid.Geometry, AllStrategies)))
        ];
    }
}
