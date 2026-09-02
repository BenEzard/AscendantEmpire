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
