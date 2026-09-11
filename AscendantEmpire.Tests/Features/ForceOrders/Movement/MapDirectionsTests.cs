using AscendantEmpire.Application.Features.ForceOrders.Movement;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.ForceOrders.Movement;

/// <summary>
/// Pure unit tests for <see cref="MapDirections"/> — no database, because it is arithmetic over a coordinate and the map's dimensions.
/// </summary>
/// <remarks>
/// The map is 10 x 8 throughout, which is small enough to reason about and big enough that a wrap and an off-by-one are told apart.
/// </remarks>
[TestFixture]
internal class MapDirectionsTests
{
    private const int _MAP_WIDTH = 10;
    private const int _MAP_HEIGHT = 8;

    /// <summary>Steps from the given coordinate on a map that does not wrap in either direction.</summary>
    private static (bool Found, Coord? Target) StepWithoutWrapping(int x, int y, MoveDirection direction)
    {
        bool found = MapDirections.TryGetNeighbour(
            new Coord(x, y),
            direction,
            _MAP_WIDTH,
            _MAP_HEIGHT,
            wrapsHorizontally: false,
            wrapsVertically: false,
            out Coord? target);

        return (found, target);
    }

    [Test, Description("Pins the eight deltas, including that North is decreasing Y — getting this backwards would send every force the wrong way.")]
    [TestCase(MoveDirection.North, 5, 3, Description = "North is up the screen, so Y decreases.")]
    [TestCase(MoveDirection.NorthEast, 6, 3, Description = "Diagonals move on both axes at once.")]
    [TestCase(MoveDirection.East, 6, 4, Description = "East increases X and leaves Y alone.")]
    [TestCase(MoveDirection.SouthEast, 6, 5, Description = "Diagonals move on both axes at once.")]
    [TestCase(MoveDirection.South, 5, 5, Description = "South is down the screen, so Y increases.")]
    [TestCase(MoveDirection.SouthWest, 4, 5, Description = "Diagonals move on both axes at once.")]
    [TestCase(MoveDirection.West, 4, 4, Description = "West decreases X and leaves Y alone.")]
    [TestCase(MoveDirection.NorthWest, 4, 3, Description = "Diagonals move on both axes at once.")]
    public void ShouldStepOneTileInEachDirection(MoveDirection direction, int expectedX, int expectedY)
    {
        (bool found, Coord? target) = StepWithoutWrapping(5, 4, direction);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(found, Is.True);
            Assert.That(target!.X, Is.EqualTo(expectedX));
            Assert.That(target.Y, Is.EqualTo(expectedY));
        }
    }

    [Test, Description("Without wrapping there is simply no tile past an edge, and the refusal has to come from here rather than a missing row later.")]
    [TestCase(0, 4, MoveDirection.West, Description = "Off the west edge.")]
    [TestCase(9, 4, MoveDirection.East, Description = "Off the east edge.")]
    [TestCase(5, 0, MoveDirection.North, Description = "Off the north edge.")]
    [TestCase(5, 7, MoveDirection.South, Description = "Off the south edge.")]
    [TestCase(0, 0, MoveDirection.NorthWest, Description = "A diagonal leaving on both axes at once.")]
    [TestCase(9, 7, MoveDirection.SouthEast, Description = "A diagonal leaving on both axes at once.")]
    public void ShouldFindNoNeighbourPastANonWrappingEdge(int x, int y, MoveDirection direction)
    {
        (bool found, Coord? target) = StepWithoutWrapping(x, y, direction);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(found, Is.False);
            Assert.That(target, Is.Null);
        }
    }

    [Test, Description("Horizontal wrapping joins the east and west edges, so stepping off one arrives at the other on the same row.")]
    [TestCase(0, 4, MoveDirection.West, 9, 4, Description = "West off column 0 arrives at the last column.")]
    [TestCase(9, 4, MoveDirection.East, 0, 4, Description = "East off the last column arrives at column 0.")]
    [TestCase(9, 4, MoveDirection.NorthEast, 0, 3, Description = "A diagonal wraps on X while still moving on Y.")]
    public void ShouldWrapHorizontallyWhenTheMapDoes(int x, int y, MoveDirection direction, int expectedX, int expectedY)
    {
        bool found = MapDirections.TryGetNeighbour(
            new Coord(x, y),
            direction,
            _MAP_WIDTH,
            _MAP_HEIGHT,
            wrapsHorizontally: true,
            wrapsVertically: false,
            out Coord? target);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(found, Is.True);
            Assert.That(target!.X, Is.EqualTo(expectedX));
            Assert.That(target.Y, Is.EqualTo(expectedY));
        }
    }

    [Test, Description("Vertical wrapping joins the north and south edges, and is independent of horizontal wrapping.")]
    [TestCase(5, 0, MoveDirection.North, 5, 7, Description = "North off row 0 arrives at the last row.")]
    [TestCase(5, 7, MoveDirection.South, 5, 0, Description = "South off the last row arrives at row 0.")]
    public void ShouldWrapVerticallyWhenTheMapDoes(int x, int y, MoveDirection direction, int expectedX, int expectedY)
    {
        bool found = MapDirections.TryGetNeighbour(
            new Coord(x, y),
            direction,
            _MAP_WIDTH,
            _MAP_HEIGHT,
            wrapsHorizontally: false,
            wrapsVertically: true,
            out Coord? target);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(found, Is.True);
            Assert.That(target!.X, Is.EqualTo(expectedX));
            Assert.That(target.Y, Is.EqualTo(expectedY));
        }
    }

    [Test, Description("A corner diagonal on a map wrapping both ways has to wrap both axes, not just whichever is checked first.")]
    public void ShouldWrapBothAxesAtOnceFromACorner()
    {
        bool found = MapDirections.TryGetNeighbour(
            new Coord(0, 0),
            MoveDirection.NorthWest,
            _MAP_WIDTH,
            _MAP_HEIGHT,
            wrapsHorizontally: true,
            wrapsVertically: true,
            out Coord? target);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(found, Is.True);
            Assert.That(target!.X, Is.EqualTo(9));
            Assert.That(target.Y, Is.EqualTo(7));
        }
    }

    [Test, Description("An interior tile has all eight neighbours, which is the ring the fog-of-war read hands a blinded observer.")]
    public void ShouldReturnAllEightNeighboursOfAnInteriorTile()
    {
        IReadOnlyCollection<Coord> neighbours = MapDirections.GetNeighbours(
            new Coord(5, 4),
            _MAP_WIDTH,
            _MAP_HEIGHT,
            wrapsHorizontally: false,
            wrapsVertically: false);

        Assert.That(neighbours, Is.EquivalentTo(new[]
        {
            new Coord(4, 3), new Coord(5, 3), new Coord(6, 3),
            new Coord(4, 4),                  new Coord(6, 4),
            new Coord(4, 5), new Coord(5, 5), new Coord(6, 5)
        }));
    }

    [Test, Description("Directions that leave a non-wrapping map are absent rather than null, so a corner tile has three neighbours and an edge five.")]
    [TestCase(0, 0, 3, Description = "The north-west corner keeps only east, south-east and south.")]
    [TestCase(9, 7, 3, Description = "The south-east corner, the other extreme of the same arithmetic.")]
    [TestCase(0, 4, 5, Description = "A western edge tile loses the three neighbours to its west.")]
    [TestCase(5, 0, 5, Description = "A northern edge tile loses the three to its north.")]
    public void ShouldOmitNeighboursThatFallOffANonWrappingEdge(int x, int y, int expectedCount)
    {
        IReadOnlyCollection<Coord> neighbours = MapDirections.GetNeighbours(
            new Coord(x, y),
            _MAP_WIDTH,
            _MAP_HEIGHT,
            wrapsHorizontally: false,
            wrapsVertically: false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(neighbours, Has.Count.EqualTo(expectedCount));
            Assert.That(neighbours.Any(neighbour => neighbour.Equals(new Coord(x, y))), Is.False, "A tile is never its own neighbour.");
        }
    }

    [Test, Description("A corner on a map wrapping both ways still has eight neighbours, three of them on the far side of the board.")]
    public void ShouldReturnAllEightNeighboursOfACornerWhenTheMapWraps()
    {
        IReadOnlyCollection<Coord> neighbours = MapDirections.GetNeighbours(
            new Coord(0, 0),
            _MAP_WIDTH,
            _MAP_HEIGHT,
            wrapsHorizontally: true,
            wrapsVertically: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(neighbours, Has.Count.EqualTo(8));
            Assert.That(neighbours, Does.Contain(new Coord(9, 7)), "North-west of the origin wraps on both axes at once.");
            Assert.That(neighbours, Does.Contain(new Coord(9, 0)), "West wraps to the far east column.");
            Assert.That(neighbours, Does.Contain(new Coord(0, 7)), "North wraps to the bottom row.");
        }
    }

    [Test, Description("Sight is measured in Chebyshev distance, so a diagonal step counts as one tile, the same as a straight one.")]
    [TestCase(7, 6, 2, Description = "Two diagonal steps.")]
    [TestCase(8, 5, 3, Description = "Three east and one south is three steps, not four.")]
    [TestCase(5, 4, 0, Description = "A tile is no distance from itself.")]
    public void ShouldCountADiagonalStepAsOneTile(int toX, int toY, int expectedDistance)
    {
        int distance = MapDirections.GetDistance(new Coord(5, 4), new Coord(toX, toY), _MAP_WIDTH, _MAP_HEIGHT, wrapsHorizontally: false, wrapsVertically: false);

        Assert.That(distance, Is.EqualTo(expectedDistance));
    }

    [Test, Description("On a wrapping map the distance goes the shorter way round the edge, and on a flat one there is no other way round.")]
    [TestCase(true, 1, Description = "Column 9 is one step west of column 0 when the edges join.")]
    [TestCase(false, 9, Description = "Without wrapping it is the whole width of the map away.")]
    public void ShouldMeasureTheShorterWayRoundOnlyWhenTheMapWraps(bool wrapsHorizontally, int expectedDistance)
    {
        int distance = MapDirections.GetDistance(new Coord(0, 4), new Coord(9, 4), _MAP_WIDTH, _MAP_HEIGHT, wrapsHorizontally, wrapsVertically: false);

        Assert.That(distance, Is.EqualTo(expectedDistance));
    }

    [Test, Description("An axis delta is folded the short way round only when the axis wraps, or a narrow map's sight tree points off its own edge.")]
    [TestCase(true, -1, Description = "On a wrapping axis four wide, three east is one west.")]
    [TestCase(false, 3, Description = "On an axis that does not wrap, three east is three east.")]
    public void ShouldFoldTheAxisDeltaOnlyWhenTheAxisWraps(bool wraps, int expectedDelta)
    {
        Assert.That(MapDirections.GetAxisDelta(from: 0, to: 3, size: 4, wraps), Is.EqualTo(expectedDelta));
    }

    [Test, Description("A direction outside the enum is a wiring fault, not a move to be refused, so it throws rather than reading as off-map.")]
    public void ShouldThrowForADirectionThatIsNotOneOfTheEight()
    {
        Assert.That(
            () => MapDirections.TryGetNeighbour(
                new Coord(5, 4),
                (MoveDirection)99,
                _MAP_WIDTH,
                _MAP_HEIGHT,
                wrapsHorizontally: false,
                wrapsVertically: false,
                out _),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }
}
