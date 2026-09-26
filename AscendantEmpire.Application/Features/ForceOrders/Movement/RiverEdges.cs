using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.ForceOrders.Movement;

/// <summary>
/// The river columns a single tile owns: its South and East edges.
/// </summary>
/// <remarks>
/// A tile's North and West edges belong to its neighbours, so these two are the whole of what any one row stores. See <see cref="RiverWidth"/>.
/// </remarks>
internal readonly record struct TileRiverEdges(RiverWidth South, RiverWidth East);

/// <summary>
/// A snapshot of every river edge in the 3×3 block of tiles around a Force, which is everything a single step can run across or alongside.
/// </summary>
/// <remarks>
/// <para>
/// Addressed by offsets relative to the origin (each of X and Y in -1, 0 or 1) rather than by map coordinates, so the river rules never need to know
/// about the map's size or its wrapping. The wrapping is resolved once, here, through <see cref="MapDirections.TryGetNeighbour"/>, so it is the same
/// arithmetic every other step calculation uses.
/// </para>
/// <para>
/// A cell that falls off a non-wrapping edge of the map, or that has no row behind it, is simply absent. Any edge touching an absent cell reads as
/// <see cref="RiverWidth.None"/>, which is also what makes a stray value on the last column's East edge harmless: that edge faces off the map, and
/// this reads it as having no river whatever the column says.
/// </para>
/// </remarks>
internal sealed class RiverEdges
{
    /// <summary>The block's tiles, indexed <c>[deltaX + 1, deltaY + 1]</c>. <see langword="null"/> where there is no tile.</summary>
    private readonly TileRiverEdges?[,] _tiles;

    private RiverEdges(TileRiverEdges?[,] tiles)
    {
        _tiles = tiles;
    }

    /// <summary>
    /// Builds the snapshot around <paramref name="origin"/> from the river columns of the tiles near it.
    /// </summary>
    /// <param name="origin">The tile the Force is standing on.</param>
    /// <param name="tilesByCoord">
    /// The river columns of at least the origin and its eight neighbours, keyed by map coordinate. Extra tiles are ignored; a missing one is read as no
    /// tile at all.
    /// </param>
    /// <param name="mapWidth">The map's width in tiles.</param>
    /// <param name="mapHeight">The map's height in tiles.</param>
    /// <param name="wrapsHorizontally">Whether the map's east and west edges join.</param>
    /// <param name="wrapsVertically">Whether the map's north and south edges join.</param>
    internal static RiverEdges Create(
        Coord origin,
        IReadOnlyDictionary<Coord, TileRiverEdges> tilesByCoord,
        int mapWidth,
        int mapHeight,
        bool wrapsHorizontally,
        bool wrapsVertically)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(tilesByCoord);

        var tiles = new TileRiverEdges?[3, 3];

        // The origin itself, then each of the eight neighbours through the shared neighbour arithmetic, so wrapping has exactly one home.
        tiles[1, 1] = tilesByCoord.TryGetValue(origin, out TileRiverEdges originEdges) ? originEdges : null;

        foreach (MoveDirection direction in Enum.GetValues<MoveDirection>())
        {
            (int deltaX, int deltaY) = MapDirections.GetDelta(direction);

            if (MapDirections.TryGetNeighbour(origin, direction, mapWidth, mapHeight, wrapsHorizontally, wrapsVertically, out Coord? neighbour)
                && tilesByCoord.TryGetValue(neighbour!, out TileRiverEdges neighbourEdges))
            {
                tiles[deltaX + 1, deltaY + 1] = neighbourEdges;
            }
        }

        return new RiverEdges(tiles);
    }

    /// <summary>
    /// The river on the edge between two orthogonally adjacent cells of the block, given as offsets from the origin.
    /// </summary>
    /// <remarks>
    /// Resolves the canonical owner of the edge: the western of the two cells owns an East edge, and the northern owns a South edge. Asking about two
    /// cells that are not orthogonal neighbours, or that lie outside the block, is a programming error rather than a question with an answer.
    /// </remarks>
    /// <returns>The edge's width, or <see cref="RiverWidth.None"/> when either cell has no tile.</returns>
    internal RiverWidth GetEdge(int fromX, int fromY, int toX, int toY)
    {
        if (!IsInBlock(fromX, fromY) || !IsInBlock(toX, toY) || Math.Abs(fromX - toX) + Math.Abs(fromY - toY) != 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(toX),
                $"({fromX}, {fromY}) and ({toX}, {toY}) are not orthogonal neighbours inside the 3x3 block around the origin.");
        }

        TileRiverEdges? from = _tiles[fromX + 1, fromY + 1];
        TileRiverEdges? to = _tiles[toX + 1, toY + 1];

        // An edge with no tile on one side faces off the map (or into a hole in it), so there is no river there to cross or follow.
        if (from is null || to is null)
        {
            return RiverWidth.None;
        }

        // Same row: the western cell owns the edge as its East. Otherwise same column: the northern cell owns it as its South.
        if (fromY == toY)
        {
            return fromX < toX ? from.Value.East : to.Value.East;
        }

        return fromY < toY ? from.Value.South : to.Value.South;
    }

    private static bool IsInBlock(int x, int y)
    {
        return x is >= -1 and <= 1 && y is >= -1 and <= 1;
    }
}
