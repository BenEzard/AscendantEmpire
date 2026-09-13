using AscendantEmpire.Application.Features.Battles.Classes;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Battles.Classes;

/// <summary>
/// Pure unit tests for <see cref="BattleDeploymentPlanner"/> — no database required, because placement is arithmetic over the map's size and offset.
/// </summary>
/// <remarks>
/// <para>
/// Most tests use a 10 x 10 map, small enough that every expected coordinate can be checked by eye. Its usable positions along an edge are 2 to 7, since the 2 x 2 corner voids take 0-1 and 8-9,
/// and its centre-out order along an edge is 4, 5, 3, 6, 2, 7.
/// </para>
/// <para>
/// The offset is deliberately not zero, so a tile id computed without it could not pass by coincidence. Tile ids are turned back into coordinates by <see cref="Coordinate"/> so each assertion
/// reads as a position rather than a number.
/// </para>
/// </remarks>
[TestFixture]
internal class BattleDeploymentPlannerTests
{
    private const int _SIZE = 10;
    private const int _OFFSET = 5_000;

    private const int _ATTACKER_ID = 1;
    private const int _DEFENDER_ID = 2;

    /// <summary>Turns a battle map tile id back into its (x, y) on the test map.</summary>
    private static (int X, int Y) Coordinate(int battleTileId, int size = _SIZE, int offset = _OFFSET)
    {
        int index = battleTileId - offset;

        return (index % size, index / size);
    }

    /// <summary>Places one attacker and one defender, and hands back where each started.</summary>
    private static ((int X, int Y) Attacker, (int X, int Y) Defender) PlaceOneEach(MoveDirection stepDirection)
    {
        IReadOnlyDictionary<int, int> placement = BattleDeploymentPlanner.Plan(_SIZE, _OFFSET, stepDirection, [_ATTACKER_ID], [_DEFENDER_ID]);

        return (Coordinate(placement[_ATTACKER_ID]), Coordinate(placement[_DEFENDER_ID]));
    }

    [Test, Description("Each side starts on the edge it should: the attacker on the side it stepped in from, the defender opposite, and a diagonal next to the corner it came through.")]
    [TestCase(MoveDirection.North, 4, 9, 4, 0, Description = "Stepping North entered from the south, so the attacker is centred on the south edge and the defender on the north.")]
    [TestCase(MoveDirection.South, 4, 0, 4, 9, Description = "Stepping South entered from the north.")]
    [TestCase(MoveDirection.East, 0, 4, 9, 4, Description = "Stepping East entered from the west, so the sides face each other across columns rather than rows.")]
    [TestCase(MoveDirection.West, 9, 4, 0, 4, Description = "Stepping West entered from the east.")]
    [TestCase(MoveDirection.NorthEast, 2, 9, 7, 0, Description = "Stepping North-East came through the south-west corner; the defender is at the north-east corner.")]
    [TestCase(MoveDirection.NorthWest, 7, 9, 2, 0, Description = "Stepping North-West came through the south-east corner; the defender is at the north-west corner.")]
    [TestCase(MoveDirection.SouthEast, 2, 0, 7, 9, Description = "Stepping South-East came through the north-west corner; the defender is at the south-east corner.")]
    [TestCase(MoveDirection.SouthWest, 7, 0, 2, 9, Description = "Stepping South-West came through the north-east corner; the defender is at the south-west corner.")]
    public void ShouldPlaceEachSideOnTheEdgeMatchingTheStep(MoveDirection stepDirection, int attackerX, int attackerY, int defenderX, int defenderY)
    {
        ((int X, int Y) attacker, (int X, int Y) defender) = PlaceOneEach(stepDirection);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(attacker, Is.EqualTo((attackerX, attackerY)), "Attacker's starting tile.");
            Assert.That(defender, Is.EqualTo((defenderX, defenderY)), "Defender's starting tile.");
        }
    }

    [Test, Description("Along a cardinal edge units spread out from the centre, alternating sides, so a lone Force meets its enemy head-on rather than from a flank.")]
    public void ShouldSpreadUnitsOutFromTheCentreOfACardinalEdge()
    {
        int[] defenderIds = [10, 11, 12, 13, 14, 15];

        IReadOnlyDictionary<int, int> placement = BattleDeploymentPlanner.Plan(_SIZE, _OFFSET, MoveDirection.North, [_ATTACKER_ID], defenderIds);

        // The defenders are on the north edge (y = 0), in the order they were listed.
        Assert.That(defenderIds.Select(id => Coordinate(placement[id])), Is.EqualTo(new[] { (4, 0), (5, 0), (3, 0), (6, 0), (2, 0), (7, 0) }));
    }

    [Test, Description("The 2 x 2 corner voids can never be entered, so a full edge must stop short of them at both ends.")]
    public void ShouldNeverPlaceAUnitInACornerVoid()
    {
        int[] defenderIds = [.. Enumerable.Range(100, 30)];

        IReadOnlyDictionary<int, int> placement = BattleDeploymentPlanner.Plan(_SIZE, _OFFSET, MoveDirection.North, [_ATTACKER_ID], defenderIds);

        Assert.That(placement.Values.Select(tileId => Coordinate(tileId)), Has.None.Matches<(int X, int Y)>(tile => (tile.X < 2 || tile.X > 7) && (tile.Y < 2 || tile.Y > 7)));
    }

    [Test, Description("When an edge fills up the zone moves one row further in, rather than stacking two units on one tile or giving up.")]
    public void ShouldMoveOneRowInWhenTheEdgeIsFull()
    {
        // Six fit on the edge of a 10 x 10 map, so the seventh starts the next row.
        int[] defenderIds = [.. Enumerable.Range(100, 7)];

        IReadOnlyDictionary<int, int> placement = BattleDeploymentPlanner.Plan(_SIZE, _OFFSET, MoveDirection.South, [_ATTACKER_ID], defenderIds);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(defenderIds.Take(6).Select(id => Coordinate(placement[id]).Y), Is.All.EqualTo(9), "The first six fill the south edge.");
            Assert.That(Coordinate(placement[defenderIds[6]]), Is.EqualTo((4, 8)), "The seventh starts the next row in, back at the centre.");
            Assert.That(placement.Values, Is.Unique, "No two units share a tile.");
        }
    }

    [Test, Description("A diagonal zone fills away from its corner, so the second unit stands beside the first rather than across the map.")]
    public void ShouldFillADiagonalZoneAwayFromItsCorner()
    {
        // Ids clear of the defender's, so no unit is listed on both sides.
        int[] attackerIds = [10, 11, 12];

        IReadOnlyDictionary<int, int> placement = BattleDeploymentPlanner.Plan(_SIZE, _OFFSET, MoveDirection.NorthEast, attackerIds, [_DEFENDER_ID]);

        Assert.That(attackerIds.Select(id => Coordinate(placement[id])), Is.EqualTo(new[] { (2, 9), (3, 9), (4, 9) }));
    }

    [Test, Description("An even-sized map has two middle positions; the lower wins the tie, so the placement is fully determined rather than resting on rounding.")]
    public void ShouldBreakTheCentreTieOnAnEvenSizedMapTowardsTheLowerPosition()
    {
        IReadOnlyDictionary<int, int> placement = BattleDeploymentPlanner.Plan(30, _OFFSET, MoveDirection.North, [_ATTACKER_ID], [_DEFENDER_ID]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Coordinate(placement[_ATTACKER_ID], size: 30), Is.EqualTo((14, 29)));
            Assert.That(Coordinate(placement[_DEFENDER_ID], size: 30), Is.EqualTo((14, 0)));
        }
    }

    [Test, Description("The id is offset + y * size + x, the same row-major addressing the battle map generator writes tiles with, so a unit's tile id finds its tile.")]
    public void ShouldAddressTilesRowMajorFromTheOffset()
    {
        IReadOnlyDictionary<int, int> placement = BattleDeploymentPlanner.Plan(_SIZE, _OFFSET, MoveDirection.North, [_ATTACKER_ID], [_DEFENDER_ID]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(placement[_ATTACKER_ID], Is.EqualTo(_OFFSET + (9 * _SIZE) + 4), "South edge, centre.");
            Assert.That(placement[_DEFENDER_ID], Is.EqualTo(_OFFSET + 4), "North edge, centre.");
        }
    }

    [Test, Description("A side too big for half the map is refused rather than being allowed to spill into the other side's zone and share their tiles.")]
    public void ShouldRefuseASideTooBigForHalfTheMap()
    {
        // Six per row for five rows is the most one side can hold on a 10 x 10 map.
        int[] defenderIds = [.. Enumerable.Range(100, 31)];

        Assert.Throws<InvalidOperationException>(() => BattleDeploymentPlanner.Plan(_SIZE, _OFFSET, MoveDirection.North, [_ATTACKER_ID], defenderIds));
    }

    [Test, Description("A unit on both sides, or listed twice, cannot be given one starting tile, so it is refused rather than silently placed once.")]
    public void ShouldRefuseAUnitListedTwice()
    {
        Assert.Throws<ArgumentException>(() => BattleDeploymentPlanner.Plan(_SIZE, _OFFSET, MoveDirection.North, [_ATTACKER_ID], [_DEFENDER_ID, _ATTACKER_ID]));
    }

    [Test, Description("A map with nothing outside its corner voids has nowhere to put anyone, which is a configuration fault worth failing loudly on.")]
    public void ShouldRefuseAMapWithNoRoomOutsideTheCornerVoids()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BattleDeploymentPlanner.Plan(4, _OFFSET, MoveDirection.North, [_ATTACKER_ID], [_DEFENDER_ID]));
    }
}
