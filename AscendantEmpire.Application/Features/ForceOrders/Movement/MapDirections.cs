using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.ForceOrders.Movement;

/// <summary>
/// Resolves a <see cref="MoveDirection"/> into the neighbouring tile's coordinate, honouring the map's wrapping.
/// </summary>
/// <remarks>
/// <para>
/// The same eight-way delta table and modulo wrap live inside <c>PathFinder</c> as private helpers, but that class is a whole-path Dijkstra search
/// that is not registered in DI and has no production caller. A single step needs only this much, so it lives here and <c>PathFinder</c> can be
/// pointed at it if and when that search is revived.
/// </para>
/// <para>
/// Coordinates are map coordinates: X grows east, Y grows south. <see cref="MapMethods"/> deliberately keeps to id/coordinate conversion, which is
/// a different concern.
/// </para>
/// </remarks>
internal static class MapDirections
{
    /// <summary>
    /// The (X, Y) offset one step in each direction.
    /// </summary>
    private static readonly IReadOnlyDictionary<MoveDirection, (int DeltaX, int DeltaY)> _DELTAS_BY_DIRECTION =
        new Dictionary<MoveDirection, (int DeltaX, int DeltaY)>
        {
            [MoveDirection.North] = (0, -1),
            [MoveDirection.NorthEast] = (1, -1),
            [MoveDirection.East] = (1, 0),
            [MoveDirection.SouthEast] = (1, 1),
            [MoveDirection.South] = (0, 1),
            [MoveDirection.SouthWest] = (-1, 1),
            [MoveDirection.West] = (-1, 0),
            [MoveDirection.NorthWest] = (-1, -1),
        };

    /// <summary>
    /// Finds the tile one step from <paramref name="origin"/> in the given direction.
    /// </summary>
    /// <param name="origin">Where the Force currently stands.</param>
    /// <param name="direction">The direction to step in.</param>
    /// <param name="mapWidth">The map's width in tiles.</param>
    /// <param name="mapHeight">The map's height in tiles.</param>
    /// <param name="wrapsHorizontally">Whether the map's east and west edges join.</param>
    /// <param name="wrapsVertically">Whether the map's north and south edges join.</param>
    /// <param name="target">The neighbouring coordinate, or <see langword="null"/> when the step leaves the map.</param>
    /// <returns>
    /// <see langword="false"/> when the step runs off a non-wrapping edge, in which case there is no such neighbour and the caller should refuse the
    /// move. Otherwise <see langword="true"/>, with <paramref name="target"/> set.
    /// </returns>
    internal static bool TryGetNeighbour(
        Coord origin,
        MoveDirection direction,
        int mapWidth,
        int mapHeight,
        bool wrapsHorizontally,
        bool wrapsVertically,
        out Coord? target)
    {
        ArgumentNullException.ThrowIfNull(origin);

        // An unrecognised direction is a programming error rather than a refusable move, so it is not silently treated as "off the map".
        if (!_DELTAS_BY_DIRECTION.TryGetValue(direction, out (int DeltaX, int DeltaY) delta))
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "There is no step defined for this direction.");
        }

        int nextX = origin.X + delta.DeltaX;
        int nextY = origin.Y + delta.DeltaY;

        if (nextX < 0 || nextX >= mapWidth)
        {
            if (!wrapsHorizontally)
            {
                target = null;
                return false;
            }

            nextX = Modulo(nextX, mapWidth);
        }

        if (nextY < 0 || nextY >= mapHeight)
        {
            if (!wrapsVertically)
            {
                target = null;
                return false;
            }

            nextY = Modulo(nextY, mapHeight);
        }

        target = new Coord(nextX, nextY);
        return true;
    }

    /// <summary>
    /// A modulo that returns a non-negative result, so stepping west off column 0 lands on the far east column.
    /// </summary>
    private static int Modulo(int value, int modulo)
    {
        return ((value % modulo) + modulo) % modulo;
    }
}
