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
    /// The (X, Y) offset one step in the given direction, before any wrapping.
    /// </summary>
    /// <remarks>
    /// Exposed for rules that reason about a step's shape rather than its destination — the river rules need to know which edges a step runs across
    /// and alongside — so they read the same table the neighbour arithmetic does.
    /// </remarks>
    internal static (int DeltaX, int DeltaY) GetDelta(MoveDirection direction)
    {
        // An unrecognised direction is a programming error, exactly as it is in TryGetNeighbour.
        if (!_DELTAS_BY_DIRECTION.TryGetValue(direction, out (int DeltaX, int DeltaY) delta))
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "There is no step defined for this direction.");
        }

        return delta;
    }

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
    /// Every tile adjacent to <paramref name="origin"/> — the eight-way ring, honouring the map's wrapping.
    /// </summary>
    /// <param name="origin">The tile to find the neighbours of.</param>
    /// <param name="mapWidth">The map's width in tiles.</param>
    /// <param name="mapHeight">The map's height in tiles.</param>
    /// <param name="wrapsHorizontally">Whether the map's east and west edges join.</param>
    /// <param name="wrapsVertically">Whether the map's north and south edges join.</param>
    /// <returns>
    /// Between three and eight coordinates. Directions that run off a non-wrapping edge are simply absent, so a tile in a corner returns three.
    /// </returns>
    /// <remarks>
    /// Built from <see cref="TryGetNeighbour"/> rather than from the delta table directly, so the wrapping arithmetic has exactly one home. The
    /// fog-of-war read uses this for the ring around an Observer that dense terrain has blinded.
    /// </remarks>
    internal static IReadOnlyCollection<Coord> GetNeighbours(
        Coord origin,
        int mapWidth,
        int mapHeight,
        bool wrapsHorizontally,
        bool wrapsVertically)
    {
        ArgumentNullException.ThrowIfNull(origin);

        List<Coord> neighbours = [];

        foreach (MoveDirection direction in _DELTAS_BY_DIRECTION.Keys)
        {
            if (TryGetNeighbour(origin, direction, mapWidth, mapHeight, wrapsHorizontally, wrapsVertically, out Coord? neighbour))
            {
                neighbours.Add(neighbour!);
            }
        }

        // A map that wraps on an axis one tile wide would fold a direction back onto the origin, and a two-wide one would fold two directions onto
        // the same tile. Neither is a real map, but the caller gets a set either way rather than a duplicate it has to notice.
        return [.. neighbours.Distinct()];
    }

    /// <summary>
    /// How many tiles apart two coordinates are, counting a diagonal step as one and taking the shorter way round any edge that wraps.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Chebyshev distance, which is the metric sight is measured in: an Observer with a Sight of 2 sees the whole five-by-five square around it, corners
    /// included, subject to what blocks the view.
    /// </para>
    /// <para>
    /// Both coordinates must already be on the map. Nothing here wraps them first.
    /// </para>
    /// </remarks>
    internal static int GetDistance(
        Coord from,
        Coord to,
        int mapWidth,
        int mapHeight,
        bool wrapsHorizontally,
        bool wrapsVertically)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        int deltaX = GetAxisDelta(from.X, to.X, mapWidth, wrapsHorizontally);
        int deltaY = GetAxisDelta(from.Y, to.Y, mapHeight, wrapsVertically);

        return Math.Max(Math.Abs(deltaX), Math.Abs(deltaY));
    }

    /// <summary>
    /// Works out which of the eight directions leads from one tile to an adjacent one, taking the shorter way round any edge that wraps.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The inverse of <see cref="TryGetNeighbour"/>, and built on the same delta table and <see cref="GetAxisDelta"/>, so a step and its direction can never disagree. The intelligence fold
    /// uses it for the direction a watched Force left in.
    /// </para>
    /// <para>
    /// A "try" rather than a throw: a pair that is not adjacent is a history the fold cannot name a direction for, and that is reported as no direction rather than as a failure that would stop a
    /// Turn from ending.
    /// </para>
    /// </remarks>
    /// <param name="from">The tile stepped off, already on the map.</param>
    /// <param name="to">The tile stepped onto, already on the map.</param>
    /// <param name="mapWidth">The map's width in tiles.</param>
    /// <param name="mapHeight">The map's height in tiles.</param>
    /// <param name="direction">The direction of the step, or <see cref="MoveDirection.North"/> when there is none.</param>
    /// <returns><see langword="false"/> when the tiles are not neighbours, including when they are the same tile.</returns>
    internal static bool TryGetStepDirection(
        Coord from,
        Coord to,
        int mapWidth,
        int mapHeight,
        bool wrapsHorizontally,
        bool wrapsVertically,
        out MoveDirection direction)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        int deltaX = GetAxisDelta(from.X, to.X, mapWidth, wrapsHorizontally);
        int deltaY = GetAxisDelta(from.Y, to.Y, mapHeight, wrapsVertically);

        foreach ((MoveDirection candidate, (int DeltaX, int DeltaY) candidateDelta) in _DELTAS_BY_DIRECTION)
        {
            if (candidateDelta.DeltaX == deltaX && candidateDelta.DeltaY == deltaY)
            {
                direction = candidate;
                return true;
            }
        }

        // Not one of the eight single steps - the same tile, or further away than one step.
        direction = default;
        return false;
    }

    /// <summary>
    /// The signed step count from <paramref name="from"/> to <paramref name="to"/> along one axis, going the shorter way round when that axis wraps.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On a wrapping axis a difference of more than half the map is quicker the other way, so it is folded back: on a map ten wide, column 9 is one step
    /// <i>west</i> of column 0, not nine steps east.
    /// </para>
    /// <para>
    /// On an axis that does not wrap there is no other way round, so the plain difference is returned. Folding it anyway sends the answer the wrong way
    /// on a map narrower than twice the distance involved — the sight tree used to do exactly that, and pointed tiles at parents off the edge of the map.
    /// </para>
    /// </remarks>
    /// <param name="from">A position along the axis, already on the map.</param>
    /// <param name="to">A position along the axis, already on the map.</param>
    /// <param name="size">The map's extent along this axis, in tiles.</param>
    internal static int GetAxisDelta(int from, int to, int size, bool wraps)
    {
        int difference = to - from;

        if (wraps == false)
        {
            return difference;
        }

        if (Math.Abs(difference) > size / 2)
        {
            difference = difference > 0 ? difference - size : difference + size;
        }

        return difference;
    }

    /// <summary>
    /// A modulo that returns a non-negative result, so stepping west off column 0 lands on the far east column.
    /// </summary>
    internal static int Modulo(int value, int modulo)
    {
        return ((value % modulo) + modulo) % modulo;
    }
}
