using AscendantEmpire.Application.Features.ForceOrders.Movement;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

/// <summary>
/// Turns the river columns a tile owns into the four edges the client is told about.
/// </summary>
/// <remarks>
/// <para>
/// A tile stores only its South and East edges, so its North edge is the <c>SouthRiver</c> of the tile above it and its West edge is the
/// <c>EastRiver</c> of the tile to its left. Resolving that here means the client never has to know which side of an edge owns it, and it is the
/// reason an edge becomes visible as soon as <b>either</b> of the tiles it separates is explored: both of them describe it.
/// </para>
/// <para>
/// Pure, and deliberately separate from the query that loads the columns — the wrapping arithmetic is the interesting part and it is testable with no
/// database at all. Wrapping goes through <see cref="MapDirections.TryGetNeighbour"/>, the same neighbour arithmetic movement and sight use, so a
/// river can never disagree with a step about which tiles are next to each other.
/// </para>
/// </remarks>
internal static class TileRiverDerivation
{
    /// <summary>
    /// The four river edges of the tile at <paramref name="coord"/>.
    /// </summary>
    /// <remarks>
    /// An edge whose owning tile is absent from <paramref name="ownedEdgesByCoord"/> reads as <see cref="RiverWidth.None"/>. That covers both an edge
    /// facing off a non-wrapping edge of the map — which must have no river — and a neighbour whose columns were simply not loaded.
    /// </remarks>
    /// <param name="coord">The tile to describe, already on the map.</param>
    /// <param name="ownedEdgesByCoord">The South and East columns of at least this tile and the tiles above and to the left of it.</param>
    /// <param name="geometry">The map's size and wrapping.</param>
    internal static TileRivers Derive(Coord coord, IReadOnlyDictionary<Coord, TileRiverEdges> ownedEdgesByCoord, CampaignMapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(coord);
        ArgumentNullException.ThrowIfNull(ownedEdgesByCoord);
        ArgumentNullException.ThrowIfNull(geometry);

        TileRiverEdges own = Lookup(coord, ownedEdgesByCoord);

        return new TileRivers
        {
            // The tile's own two columns, which is the whole of what its row stores.
            South = own.South,
            East = own.East,

            // The other two belong to its neighbours, and are read off them rather than duplicated onto this row.
            North = NeighbourEdge(coord, MoveDirection.North, ownedEdgesByCoord, geometry, edges => edges.South),
            West = NeighbourEdge(coord, MoveDirection.West, ownedEdgesByCoord, geometry, edges => edges.East)
        };
    }

    /// <summary>
    /// Which tiles' columns have to be loaded to describe every tile in <paramref name="coords"/>.
    /// </summary>
    /// <remarks>
    /// Each requested tile, plus the tile above it and the tile to its left, because those two own its North and West edges. Duplicates collapse, so
    /// asking about a block of tiles costs barely more than the block itself.
    /// </remarks>
    internal static IReadOnlyCollection<Coord> GetCoordsToLoad(IEnumerable<Coord> coords, CampaignMapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(coords);
        ArgumentNullException.ThrowIfNull(geometry);

        HashSet<Coord> needed = [];

        foreach (Coord coord in coords)
        {
            needed.Add(coord);

            foreach (MoveDirection direction in (MoveDirection[])[MoveDirection.North, MoveDirection.West])
            {
                if (TryGetNeighbour(coord, direction, geometry, out Coord? neighbour))
                {
                    needed.Add(neighbour!);
                }
            }
        }

        return needed;
    }

    /// <summary>
    /// The edge owned by the neighbour in <paramref name="direction"/>, or <see cref="RiverWidth.None"/> when there is no such tile.
    /// </summary>
    private static RiverWidth NeighbourEdge(
        Coord coord,
        MoveDirection direction,
        IReadOnlyDictionary<Coord, TileRiverEdges> ownedEdgesByCoord,
        CampaignMapGeometry geometry,
        Func<TileRiverEdges, RiverWidth> selectEdge)
    {
        if (!TryGetNeighbour(coord, direction, geometry, out Coord? neighbour))
        {
            return RiverWidth.None;
        }

        return selectEdge(Lookup(neighbour!, ownedEdgesByCoord));
    }

    private static bool TryGetNeighbour(Coord coord, MoveDirection direction, CampaignMapGeometry geometry, out Coord? neighbour)
    {
        return MapDirections.TryGetNeighbour(
            coord,
            direction,
            geometry.Width,
            geometry.Height,
            geometry.WrapsHorizontally,
            geometry.WrapsVertically,
            out neighbour);
    }

    private static TileRiverEdges Lookup(Coord coord, IReadOnlyDictionary<Coord, TileRiverEdges> ownedEdgesByCoord)
    {
        return ownedEdgesByCoord.TryGetValue(coord, out TileRiverEdges edges) ? edges : default;
    }
}
