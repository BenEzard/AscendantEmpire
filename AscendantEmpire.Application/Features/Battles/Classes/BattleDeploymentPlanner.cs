using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Battles.Classes;

/// <summary>
/// Works out which battle map tile each Unit starts a Battle on: the attackers along the edge they entered from, and the defenders along the edge opposite.
/// </summary>
/// <remarks>
/// <para>
/// An interim rule, standing in for the starting positions described in <c>docs/design/battlefield-maps.md</c> until a placement step exists. That design has the assaulter placed along the
/// side it entered from, which this keeps, and the defender placed according to what it was doing when it was assaulted (resting, fortifying, ambushing or hiding), which this does not yet
/// model - every defender is simply placed along the far edge.
/// </para>
/// <para>
/// The step direction is the direction the attacker <b>moved</b>, so the edge it entered by is the one behind it: a Force stepping North entered its new tile from the south, and starts on
/// the south edge. A diagonal step entered through a corner, so the attackers start along the north or south edge next to that corner and fill away from it, and the defenders take the
/// diagonally opposite corner in the same way.
/// </para>
/// <para>
/// Every placement is one Unit per tile. Along a cardinal edge Units spread outwards from the centre, so a lone Force meets its enemy head-on rather than from one flank. The 2 x 2 void at each
/// corner of the map is never used, and a zone that runs out of room along its edge moves one row (or column) further in. A zone never reaches the middle of the map, so the two sides can never
/// be given the same tile.
/// </para>
/// <para>
/// Pure and deterministic: the same inputs always give the same placement, which is what lets it be tested without a database and what keeps a re-run from reshuffling anyone.
/// </para>
/// <para>
/// <b>Not considered yet:</b> whether a battle tile can actually be stood on. A land Force may be placed on a lake tile, for example. That belongs with the placement design.
/// </para>
/// </remarks>
internal static class BattleDeploymentPlanner
{
    /// <summary>
    /// The width, in tiles, of the void square at each corner of a battle map, which no Unit may be placed in.
    /// </summary>
    /// <remarks>See "Tile boundaries" in <c>docs/design/battlefield-maps.md</c>.</remarks>
    private const int _CORNER_VOID_WIDTH = 2;

    /// <summary>
    /// Places every Unit on both sides of a new Battle.
    /// </summary>
    /// <param name="battleMapSize">The width and height of the (always square) battle map, from <c>GameIndex.BattleMapSize</c>.</param>
    /// <param name="battleMapOffsetId">The id of the battle map's tile (0, 0), from <c>BattleMapIndex.BattleMapOffsetId</c>.</param>
    /// <param name="stepDirection">The direction the attacking Force stepped to enter the campaign tile.</param>
    /// <param name="attackerUnitIds">The attacking Units, in the order they should be placed.</param>
    /// <param name="defenderUnitIds">The defending Units, in the order they should be placed.</param>
    /// <returns>The battle map tile id for every Unit, keyed by Unit id. Tile (x, y) is <c>battleMapOffsetId + (y * battleMapSize + x)</c>, with y = 0 as the north edge.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The map is too small to have any tile outside its corner voids, or the direction is not one of the eight.</exception>
    /// <exception cref="ArgumentException">A Unit is listed more than once.</exception>
    /// <exception cref="InvalidOperationException">A side has more Units than half the map can hold.</exception>
    public static IReadOnlyDictionary<int, int> Plan(
        int battleMapSize,
        int battleMapOffsetId,
        MoveDirection stepDirection,
        IReadOnlyList<int> attackerUnitIds,
        IReadOnlyList<int> defenderUnitIds)
    {
        ArgumentNullException.ThrowIfNull(attackerUnitIds);
        ArgumentNullException.ThrowIfNull(defenderUnitIds);

        // With both corner voids taken out of an edge there has to be at least one tile left to stand on.
        if (battleMapSize <= _CORNER_VOID_WIDTH * 2)
        {
            throw new ArgumentOutOfRangeException(nameof(battleMapSize), battleMapSize, $"A battle map must be wider than {_CORNER_VOID_WIDTH * 2} tiles to have room outside its corner voids.");
        }

        (DeploymentZone attackerZone, DeploymentZone defenderZone) = GetZones(stepDirection);

        var battleTileIdsByUnitId = new Dictionary<int, int>();

        Deploy(attackerUnitIds, attackerZone, battleMapSize, battleMapOffsetId, battleTileIdsByUnitId);
        Deploy(defenderUnitIds, defenderZone, battleMapSize, battleMapOffsetId, battleTileIdsByUnitId);

        return battleTileIdsByUnitId;
    }

    /// <summary>
    /// Picks the edge, and the order along it, that each side is placed in.
    /// </summary>
    private static (DeploymentZone Attacker, DeploymentZone Defender) GetZones(MoveDirection stepDirection)
    {
        return stepDirection switch
        {
            // Cardinal steps: the attacker is behind where it stepped, the defender faces it across the map, and both are centred on their edge.
            MoveDirection.North => (new DeploymentZone(BattleMapEdge.South, LaneOrder.CentreOut), new DeploymentZone(BattleMapEdge.North, LaneOrder.CentreOut)),
            MoveDirection.South => (new DeploymentZone(BattleMapEdge.North, LaneOrder.CentreOut), new DeploymentZone(BattleMapEdge.South, LaneOrder.CentreOut)),
            MoveDirection.East => (new DeploymentZone(BattleMapEdge.West, LaneOrder.CentreOut), new DeploymentZone(BattleMapEdge.East, LaneOrder.CentreOut)),
            MoveDirection.West => (new DeploymentZone(BattleMapEdge.East, LaneOrder.CentreOut), new DeploymentZone(BattleMapEdge.West, LaneOrder.CentreOut)),

            // Diagonal steps come in through a corner. Stepping North-East entered from the south-west corner, so the attacker fills the south edge eastwards from its west end, and the
            // defender fills the north edge westwards from its east end. The other three are the same shape mirrored.
            MoveDirection.NorthEast => (new DeploymentZone(BattleMapEdge.South, LaneOrder.Ascending), new DeploymentZone(BattleMapEdge.North, LaneOrder.Descending)),
            MoveDirection.NorthWest => (new DeploymentZone(BattleMapEdge.South, LaneOrder.Descending), new DeploymentZone(BattleMapEdge.North, LaneOrder.Ascending)),
            MoveDirection.SouthEast => (new DeploymentZone(BattleMapEdge.North, LaneOrder.Ascending), new DeploymentZone(BattleMapEdge.South, LaneOrder.Descending)),
            MoveDirection.SouthWest => (new DeploymentZone(BattleMapEdge.North, LaneOrder.Descending), new DeploymentZone(BattleMapEdge.South, LaneOrder.Ascending)),

            _ => throw new ArgumentOutOfRangeException(nameof(stepDirection), stepDirection, "The step direction must be one of the eight compass directions.")
        };
    }

    /// <summary>
    /// Places one side's Units along its zone, one per tile, moving a row further in each time the edge fills up.
    /// </summary>
    private static void Deploy(IReadOnlyList<int> unitIds, DeploymentZone zone, int battleMapSize, int battleMapOffsetId, Dictionary<int, int> battleTileIdsByUnitId)
    {
        int[] lane = OrderLane(battleMapSize, zone.Order);

        // How many rows in from its edge a zone may go. Half the map, rounded down, so the two sides' zones can never meet whatever the map's size.
        int maximumDepth = battleMapSize / 2;

        for (int index = 0; index < unitIds.Count; index++)
        {
            // The first lane-width of Units stand on the edge itself, the next lane-width one row in, and so on.
            int depth = index / lane.Length;

            if (depth >= maximumDepth)
            {
                throw new InvalidOperationException($"{unitIds.Count} Units cannot all be placed on one side of a {battleMapSize} x {battleMapSize} battle map.");
            }

            int alongEdge = lane[index % lane.Length];

            // The lane runs along the edge; the depth runs away from it towards the middle of the map. North is y = 0, as it is on the campaign map.
            (int x, int y) = zone.Edge switch
            {
                BattleMapEdge.North => (alongEdge, depth),
                BattleMapEdge.South => (alongEdge, battleMapSize - 1 - depth),
                BattleMapEdge.West => (depth, alongEdge),
                BattleMapEdge.East => (battleMapSize - 1 - depth, alongEdge),
                _ => throw new ArgumentOutOfRangeException(nameof(zone), zone.Edge, "Unknown battle map edge.")
            };

            if (!battleTileIdsByUnitId.TryAdd(unitIds[index], battleMapOffsetId + (y * battleMapSize) + x))
            {
                throw new ArgumentException($"Unit Id {unitIds[index]} is listed more than once, so it cannot be given a single starting tile.", nameof(unitIds));
            }
        }
    }

    /// <summary>
    /// Lists the positions along an edge that a Unit may stand on, in the order they are filled.
    /// </summary>
    /// <remarks>The corner voids are left off both ends, so every position returned is usable on any row of the zone.</remarks>
    private static int[] OrderLane(int battleMapSize, LaneOrder order)
    {
        int first = _CORNER_VOID_WIDTH;
        int last = battleMapSize - 1 - _CORNER_VOID_WIDTH;

        IEnumerable<int> positions = Enumerable.Range(first, last - first + 1);

        return order switch
        {
            LaneOrder.Ascending => [.. positions],
            LaneOrder.Descending => [.. positions.Reverse()],

            // Distance from the centre is measured doubled, (2p - (size - 1)), so an even-sized map's two middle positions tie exactly rather than through a rounded
            // decimal. Ties go to the lower position, so the order is fully determined.
            LaneOrder.CentreOut => [.. positions.OrderBy(position => Math.Abs((2 * position) - (battleMapSize - 1))).ThenBy(position => position)],

            _ => throw new ArgumentOutOfRangeException(nameof(order), order, "Unknown lane order.")
        };
    }

    /// <summary>Which edge of the battle map a side is placed along.</summary>
    private enum BattleMapEdge
    {
        North,
        South,
        West,
        East
    }

    /// <summary>The order the positions along an edge are filled in.</summary>
    private enum LaneOrder
    {
        /// <summary>From the middle of the edge outwards, alternating sides.</summary>
        CentreOut,

        /// <summary>From the low-numbered end (west, or north) of the edge.</summary>
        Ascending,

        /// <summary>From the high-numbered end (east, or south) of the edge.</summary>
        Descending
    }

    /// <summary>Where one side of a Battle is placed: an edge, and the order positions along it are filled.</summary>
    private readonly record struct DeploymentZone(BattleMapEdge Edge, LaneOrder Order);
}
