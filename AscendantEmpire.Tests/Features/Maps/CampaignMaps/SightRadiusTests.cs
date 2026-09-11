using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;

namespace AscendantEmpire.Tests.Features.Maps.CampaignMaps;

/// <summary>
/// Pure unit tests for <see cref="SightRadius"/> — no database, because the sight tree is geometry over a coordinate and the map's shape.
/// </summary>
/// <remarks>
/// <para>
/// The coverage cases are ported from the old <c>GetCampaignMapTileRadiusTests</c>, which exercised the same arithmetic behind a mediator call. They pin
/// that the tree covers exactly the square within range, wrapped or clipped to the map, with every tile in it once.
/// </para>
/// <para>
/// The map is 8 x 10 unless a test needs otherwise, so a wrap and an off-by-one are told apart.
/// </para>
/// </remarks>
[TestFixture]
internal class SightRadiusTests
{
    private const int _MAP_WIDTH = 8;
    private const int _MAP_HEIGHT = 10;
    private const int _MAP_OFFSET = 1;

    private static CampaignMapGeometry Geometry(bool wrapsHorizontally, bool wrapsVertically)
    {
        return new CampaignMapGeometry(_MAP_OFFSET, _MAP_WIDTH, _MAP_HEIGHT, wrapsHorizontally, wrapsVertically);
    }

    [Test, Description("The sight tree covers exactly the square in range, wrapped or clipped to the map, and nothing is listed twice.")]
    [TestCase(3, 5, 0, false, false, Description = "A range of zero has no tree at all: the occupied tile is the root, not a branch.")]
    [TestCase(3, 5, 1, false, false, Description = "The ring of eight around an interior tile.")]
    [TestCase(3, 5, 2, false, false, Description = "Two rings out, still clear of every edge.")]
    [TestCase(3, 5, 3, false, false, Description = "Three rings out, still clear of every edge.")]
    [TestCase(3, 0, 1, false, true, Description = "Over the top edge onto the bottom row when the map wraps vertically.")]
    [TestCase(3, 0, 2, false, true, Description = "Two rows over the top edge when the map wraps vertically.")]
    [TestCase(3, 9, 2, false, true, Description = "Under the bottom edge onto the top rows when the map wraps vertically.")]
    [TestCase(0, 4, 2, true, false, Description = "Off the west edge onto the east columns when the map wraps horizontally.")]
    [TestCase(7, 4, 2, true, false, Description = "Off the east edge onto the west columns when the map wraps horizontally.")]
    [TestCase(0, 0, 1, true, true, Description = "Both axes wrap at once from a corner.")]
    [TestCase(0, 0, 1, false, false, Description = "A corner of a map that does not wrap keeps only the three tiles on the board.")]
    public void ShouldCoverTheSquareInRangeWhenBuildingTheTree(int originX, int originY, int radius, bool wrapsHorizontally, bool wrapsVertically)
    {
        IReadOnlyCollection<SightNode> tree = SightRadius.BuildSightTree(new Coord(originX, originY), radius, Geometry(wrapsHorizontally, wrapsVertically));

        List<(int X, int Y)> expected = [];

        for (int y = originY - radius; y <= originY + radius; y++)
        {
            for (int x = originX - radius; x <= originX + radius; x++)
            {
                int mapX = wrapsHorizontally ? Wrap(x, _MAP_WIDTH) : x;
                int mapY = wrapsVertically ? Wrap(y, _MAP_HEIGHT) : y;

                if (mapX < 0 || mapX >= _MAP_WIDTH || mapY < 0 || mapY >= _MAP_HEIGHT)
                {
                    continue;
                }

                if (mapX == originX && mapY == originY)
                {
                    continue;
                }

                expected.Add((mapX, mapY));
            }
        }

        // EquivalentTo compares counts as well as members, so a tile reachable along two branches would fail here as surely as a missing one.
        Assert.That(Flatten(tree), Is.EquivalentTo(expected.Distinct()));
    }

    [Test, Description("A tile is seen along a path that runs diagonally until level with the Observer, then straight in, so blocking cover hides what lies behind it.")]
    public void ShouldRouteATileDiagonallyThenStraightBackToTheObserver()
    {
        IReadOnlyCollection<SightNode> tree = SightRadius.BuildSightTree(new Coord(3, 5), 3, Geometry(wrapsHorizontally: false, wrapsVertically: false));

        // (6, 7) is three east and two south: two diagonal steps back reach (4, 5), level with the Observer, and the last step is straight in.
        Assert.That(PathTo(tree, (6, 7)), Is.EqualTo(new[] { (4, 5), (5, 6), (6, 7) }));
    }

    [Test, Description("On a map narrower than the range, a tile's parent must lie between it and the Observer rather than off the edge, or the tile is never seen.")]
    public void ShouldKeepEveryParentOnTheMapWhenANarrowMapDoesNotWrap()
    {
        // Four columns and a range of three. Folding the delta the short way round - correct only on a wrapping axis - used to send (3, 1) back to a parent
        // at column 4, which is off the map, leaving the far column of a small map unseeable.
        var narrowMap = new CampaignMapGeometry(CampaignMapOffsetId: 1, Width: 4, Height: 3, WrapsHorizontally: false, WrapsVertically: false);

        IReadOnlyCollection<SightNode> tree = SightRadius.BuildSightTree(new Coord(0, 1), 3, narrowMap);

        Assert.That(PathTo(tree, (3, 1)), Is.EqualTo(new[] { (1, 1), (2, 1), (3, 1) }));
    }

    [Test, Description("The square includes its own centre, since it describes an area rather than the sight paths out of it.")]
    public void ShouldIncludeTheOriginInTheSquare()
    {
        IReadOnlyCollection<Coord> square = SightRadius.GetSquare(new Coord(3, 5), 1, Geometry(wrapsHorizontally: false, wrapsVertically: false));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(square, Has.Count.EqualTo(9));
            Assert.That(square, Does.Contain(new Coord(3, 5)));
        }
    }

    [Test, Description("A negative radius is no area at all, rather than an error the witness box has to guard against.")]
    public void ShouldReturnAnEmptySquareWhenTheRadiusIsNegative()
    {
        Assert.That(SightRadius.GetSquare(new Coord(3, 5), -1, Geometry(wrapsHorizontally: false, wrapsVertically: false)), Is.Empty);
    }

    private static int Wrap(int value, int size)
    {
        return ((value % size) + size) % size;
    }

    private static List<(int X, int Y)> Flatten(IEnumerable<SightNode> nodes)
    {
        List<(int X, int Y)> coords = [];

        foreach (SightNode node in nodes)
        {
            coords.Add((node.X, node.Y));
            coords.AddRange(Flatten(node.Children));
        }

        return coords;
    }

    /// <summary>The chain of tiles from the first ring out to <paramref name="target"/>, or empty when the tree never reaches it.</summary>
    private static List<(int X, int Y)> PathTo(IEnumerable<SightNode> nodes, (int X, int Y) target)
    {
        foreach (SightNode node in nodes)
        {
            if ((node.X, node.Y) == target)
            {
                return [(node.X, node.Y)];
            }

            List<(int X, int Y)> deeper = PathTo(node.Children, target);

            if (deeper.Count > 0)
            {
                return [(node.X, node.Y), .. deeper];
            }
        }

        return [];
    }
}
