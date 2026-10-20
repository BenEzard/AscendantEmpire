using AscendantEmpire.Application.Features.ForceOrders.Movement;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Maps.CampaignMaps;

/// <summary>
/// A whole campaign map of <see cref="TerritoryTile"/>, unclaimed Flat Grassland unless a test paints it, for exercising the territory rules without a database.
/// </summary>
/// <remarks>
/// <para>
/// The sibling of <see cref="SightTerrainGrid"/>, and the terrain facts mirror the seed data in the same way: Ocean is water and traversable, Mountainous carries the
/// impassable movement cost, and the Void is not traversable. So a rule that passes against this grid is describing terrain that exists.
/// </para>
/// <para>
/// Rivers are set by the edge a test is thinking of - including North and West, which are translated onto the neighbour that actually owns them - so a fixture never has
/// to work out which of two tiles stores a given edge.
/// </para>
/// </remarks>
internal sealed class TerritoryTestGrid
{
    private readonly Dictionary<(int X, int Y), TerritoryTile> _tiles = [];

    internal TerritoryTestGrid(CampaignMapGeometry geometry)
    {
        Geometry = geometry;

        for (int y = 0; y < geometry.Height; y++)
        {
            for (int x = 0; x < geometry.Width; x++)
            {
                _tiles[(x, y)] = BuildTile(x, y, BiomeType.Grassland, CampaignMapGeographyType.Flat);
            }
        }
    }

    internal CampaignMapGeometry Geometry { get; }

    internal TerritoryTile this[int x, int y] => _tiles[(x, y)];

    /// <summary>The world-map id of a tile, from the grid's own geometry.</summary>
    internal int IdOf(int x, int y)
    {
        return Geometry.GetCampaignMapId(new Coord(x, y));
    }

    /// <summary>Repaints one tile's biome, keeping everything else about it.</summary>
    internal TerritoryTestGrid Paint(int x, int y, BiomeType biomeType)
    {
        _tiles[(x, y)] = _tiles[(x, y)] with
        {
            BiomeTypeId = biomeType,
            BiomeIsTraversable = IsTraversable(biomeType)
        };

        return this;
    }

    /// <summary>Makes one tile a mountain - ownable, but impassable, so it seals a ring and carries no supply hop.</summary>
    internal TerritoryTestGrid Mountain(int x, int y)
    {
        _tiles[(x, y)] = _tiles[(x, y)] with { GeographyMovementCost = _IMPASSABLE_COST };

        return this;
    }

    /// <summary>Makes one tile open water, which for ownership is ordinary terrain.</summary>
    internal TerritoryTestGrid Water(int x, int y)
    {
        return Paint(x, y, BiomeType.Ocean);
    }

    /// <summary>Makes one tile the Void, which nobody can ever own or claim.</summary>
    internal TerritoryTestGrid Void(int x, int y)
    {
        return Paint(x, y, BiomeType.Void);
    }

    /// <summary>Gives a tile an owner, optionally through a Settlement.</summary>
    internal TerritoryTestGrid Own(int x, int y, int playerId, int? settlementId = null)
    {
        _tiles[(x, y)] = _tiles[(x, y)] with
        {
            OwnedByPlayerId = playerId,
            OwnedBySettlementId = settlementId,
            ClaimantPlayerIds = [],
            HasClaims = false
        };

        return this;
    }

    /// <summary>Adds a claimant to a tile. Claims are non-exclusive, so this can be called more than once for one tile.</summary>
    internal TerritoryTestGrid Claim(int x, int y, int playerId)
    {
        TerritoryTile tile = _tiles[(x, y)];

        _tiles[(x, y)] = tile with
        {
            ClaimantPlayerIds = [.. tile.ClaimantPlayerIds.Append(playerId).Distinct().Order()],
            HasClaims = true
        };

        return this;
    }

    /// <summary>Puts a rival's visible Force on a tile, which holds it whoever owns it underneath.</summary>
    internal TerritoryTestGrid Occupy(int x, int y, int playerId)
    {
        _tiles[(x, y)] = _tiles[(x, y)] with { OccupiedByPlayerId = playerId };

        return this;
    }

    /// <summary>Makes a tile Abandoned - held by nobody, but still remembering who held it.</summary>
    internal TerritoryTestGrid Abandon(int x, int y, int lastOwnerPlayerId)
    {
        _tiles[(x, y)] = _tiles[(x, y)] with
        {
            OwnedByPlayerId = null,
            LastOwnedByPlayerId = lastOwnerPlayerId
        };

        return this;
    }

    /// <summary>Sets <c>HasClaims</c> on its own, for the case where the flag has gone out of step with the junction.</summary>
    internal TerritoryTestGrid SetHasClaimsFlag(int x, int y, bool hasClaims)
    {
        _tiles[(x, y)] = _tiles[(x, y)] with { HasClaims = hasClaims };

        return this;
    }

    /// <summary>
    /// Puts a river on the named edge of a tile, whichever tile actually stores it.
    /// </summary>
    /// <remarks>
    /// A tile owns only its South and East edges: its North is the tile above's South, and its West is the tile to its left's East. Translating here is what lets a test
    /// say "a river along the west side of (3, 3)" and mean it.
    /// </remarks>
    internal TerritoryTestGrid River(int x, int y, MoveDirection edge, RiverWidth width)
    {
        (MoveDirection ownedEdge, int ownerX, int ownerY) = edge switch
        {
            MoveDirection.South => (MoveDirection.South, x, y),
            MoveDirection.East => (MoveDirection.East, x, y),
            MoveDirection.North => (MoveDirection.South, x, y - 1),
            MoveDirection.West => (MoveDirection.East, x - 1, y),
            _ => throw new ArgumentOutOfRangeException(nameof(edge), edge, "A river runs along one of a tile's four sides, never through a corner.")
        };

        ownerX = Geometry.WrapsHorizontally ? MapDirections.Modulo(ownerX, Geometry.Width) : ownerX;
        ownerY = Geometry.WrapsVertically ? MapDirections.Modulo(ownerY, Geometry.Height) : ownerY;

        TerritoryTile owner = _tiles[(ownerX, ownerY)];

        _tiles[(ownerX, ownerY)] = owner with
        {
            Rivers = ownedEdge == MoveDirection.South
                ? new TileRiverEdges(width, owner.Rivers.East)
                : new TileRiverEdges(owner.Rivers.South, width)
        };

        return this;
    }

    /// <summary>The grid as the rules see it.</summary>
    internal TerritoryMap ToMap()
    {
        return TerritoryMap.Create(Geometry, _tiles.Values);
    }

    /// <summary>Runs the whole pass over the grid.</summary>
    internal TerritoryChanges Resolve(params int[] playerIdsInTurnOrder)
    {
        return TileTerritory.Resolve(ToMap(), playerIdsInTurnOrder);
    }

    /// <summary>A working set over the grid, for a fixture exercising one rule rather than the whole pass.</summary>
    internal TerritoryWorkingSet ToWorkingSet(params int[] playerIdsInTurnOrder)
    {
        return TerritoryWorkingSet.From(ToMap(), playerIdsInTurnOrder);
    }

    /// <summary>The movement cost the seed data gives Mountainous, which is the convention for "you cannot walk here".</summary>
    private const decimal _IMPASSABLE_COST = 999m;

    private static bool IsTraversable(BiomeType biomeType)
    {
        // The Void is the absence of terrain rather than a kind of it, and is the only biome the seed data marks untraversable.
        return biomeType != BiomeType.Void;
    }

    private TerritoryTile BuildTile(int x, int y, BiomeType biomeType, CampaignMapGeographyType geographyType)
    {
        return new TerritoryTile
        {
            CampaignMapId = IdOf(x, y),
            X = x,
            Y = y,
            ClaimantPlayerIds = [],
            HasClaims = false,
            BiomeTypeId = biomeType,
            BiomeIsTraversable = IsTraversable(biomeType),
            GeographyMovementCost = geographyType == CampaignMapGeographyType.Mountainous ? _IMPASSABLE_COST : 1m,
            Rivers = new TileRiverEdges(RiverWidth.None, RiverWidth.None)
        };
    }
}
