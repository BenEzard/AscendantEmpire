using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Maps.CampaignMaps;

/// <summary>
/// Pure unit tests for <see cref="FieldOfView"/> — no database, because the sight rules read only the terrain snapshots they are handed.
/// </summary>
/// <remarks>
/// <para>
/// These pin the vision rules the live map read and the witness service now share: range, cover, high ground, blindness in dense terrain, and wrapping.
/// The end-to-end versions against a real game live in <c>PlayerCampaignMapExplorationTests</c>.
/// </para>
/// <para>
/// The map is 10 x 8, Flat Grassland unless painted, and does not wrap unless a test says so.
/// </para>
/// </remarks>
[TestFixture]
internal class FieldOfViewTests
{
    private static SightTerrainGrid NewGrid(bool wrapsHorizontally = false)
    {
        return new SightTerrainGrid(new CampaignMapGeometry(CampaignMapOffsetId: 0, Width: 10, Height: 8, WrapsHorizontally: wrapsHorizontally, WrapsVertically: false));
    }

    private static HashSet<(int X, int Y)> SeenCoords(SightTerrainGrid grid, SightObserver observer)
    {
        return [.. FieldOfView.ComputeSeenTiles(observer, grid.Terrain, grid.Geometry, SightTestData.AllStrategies).Select(tile => (tile.X, tile.Y))];
    }

    [Test, Description("With nothing in the way an Observer sees the whole square its Sight reaches, corners included.")]
    public void ShouldSeeTheWholeSquareWhenNothingBlocksTheView()
    {
        SightTerrainGrid grid = NewGrid();

        Assert.That(SeenCoords(grid, SightTestData.Unit(id: 1, playerId: 1, x: 5, y: 4, sightRange: 2)), Has.Count.EqualTo(25));
    }

    [Test, Description("The Observer's own tile is reported first, because it is always seen and is where every sight path starts.")]
    public void ShouldReportTheOccupiedTileFirst()
    {
        SightTerrainGrid grid = NewGrid();

        IReadOnlyCollection<SightTerrain> seen = FieldOfView.ComputeSeenTiles(SightTestData.Unit(1, 1, 5, 4, 2), grid.Terrain, grid.Geometry, SightTestData.AllStrategies);

        Assert.That((seen.First().X, seen.First().Y), Is.EqualTo((5, 4)));
    }

    [Test, Description("A Jungle in the line of sight is itself seen, and hides everything behind it along that path.")]
    public void ShouldSeeTheCoverButNothingBehindItWhenAJungleStandsInTheWay()
    {
        SightTerrainGrid grid = NewGrid().Paint(3, 4, BiomeType.Jungle);

        HashSet<(int X, int Y)> seen = SeenCoords(grid, SightTestData.Unit(1, 1, 2, 4, 3));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(seen, Does.Contain((3, 4)), "The Jungle tile itself is in plain view.");
            Assert.That(seen, Does.Not.Contain((4, 4)), "The tile directly behind the Jungle is hidden by it.");
            Assert.That(seen, Does.Not.Contain((5, 4)), "And so is the one behind that.");
        }
    }

    [Test, Description("Ground higher than the tile before it is seen, and blocks the view of whatever lies beyond it.")]
    public void ShouldStopTheViewBeyondHigherGroundWhenTheTerrainRises()
    {
        SightTerrainGrid grid = NewGrid().Raise(3, 4, CampaignMapGeographyType.Hilly);

        HashSet<(int X, int Y)> seen = SeenCoords(grid, SightTestData.Unit(1, 1, 2, 4, 3));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(seen, Does.Contain((3, 4)), "The hillside facing the Observer is seen.");
            Assert.That(seen, Does.Not.Contain((4, 4)), "What lies behind the hill is not.");
        }
    }

    [Test, Description("An Observer on high ground sees further by the ground's elevation, and looking down off it blocks nothing.")]
    public void ShouldWidenTheRangeWhenTheObserverStandsOnHighGround()
    {
        SightTerrainGrid grid = NewGrid().Raise(5, 4, CampaignMapGeographyType.Hilly);

        Assert.That(SeenCoords(grid, SightTestData.Unit(1, 1, 5, 4, 1)), Has.Count.EqualTo(25), "A Sight of 1 on Hilly ground sees two rings out.");
    }

    [Test, Description("An Observer blinded by its own Jungle sees its tile and the sea it could never walk to, and none of the land it could.")]
    public void ShouldSeeOnlyItsOwnTileAndTheBarredNeighboursWhenBlindedByAJungle()
    {
        SightTerrainGrid grid = NewGrid().Paint(5, 4, BiomeType.Jungle).Paint(6, 4, BiomeType.Ocean);

        Assert.That(SeenCoords(grid, SightTestData.Unit(1, 1, 5, 4, 3)), Is.EquivalentTo(new[] { (5, 4), (6, 4) }));
    }

    [Test, Description("A negative Sight is not an Observer at all, so it does not even see the tile it stands on.")]
    public void ShouldSeeNothingWhenTheSightRangeIsNegative()
    {
        Assert.That(SeenCoords(NewGrid(), SightTestData.Unit(1, 1, 5, 4, -1)), Is.Empty);
    }

    [Test, Description("An Observer standing on ground the caller could not read sees nothing, rather than failing the whole read.")]
    public void ShouldSeeNothingWhenTheOccupiedTileIsMissingFromTheTerrain()
    {
        SightTerrainGrid grid = NewGrid();
        Dictionary<(int X, int Y), SightTerrain> terrainWithoutTheTile = grid.Terrain.Where(entry => entry.Key != (5, 4)).ToDictionary();

        IReadOnlyCollection<SightTerrain> seen = FieldOfView.ComputeSeenTiles(SightTestData.Unit(1, 1, 5, 4, 2), terrainWithoutTheTile, grid.Geometry, SightTestData.AllStrategies);

        Assert.That(seen, Is.Empty);
    }

    [Test, Description("On a map that wraps, the view carries across the edge onto the far side of the board.")]
    public void ShouldSeeAcrossTheEdgeWhenTheMapWraps()
    {
        HashSet<(int X, int Y)> seen = SeenCoords(NewGrid(wrapsHorizontally: true), SightTestData.Unit(1, 1, 0, 4, 1));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(seen, Has.Count.EqualTo(9));
            Assert.That(seen, Does.Contain((9, 4)), "West of column 0 is the last column.");
        }
    }

    [Test, Description("A blinded Observer's footprint is its own tile and the ring around it, so none of the square it cannot see is loaded.")]
    public void ShouldNeedOnlyTheRingOfNeighboursWhenTheObserverIsBlinded()
    {
        SightTerrainGrid grid = NewGrid().Paint(5, 4, BiomeType.Jungle);

        IReadOnlyCollection<Coord> footprint = FieldOfView.GetFootprint(SightTestData.Unit(1, 1, 5, 4, 3), grid[5, 4], grid.Geometry);

        Assert.That(footprint, Has.Count.EqualTo(9));
    }

    [Test, Description("The footprint follows the range resolved against the occupied tile, so high ground widens what has to be loaded.")]
    public void ShouldWidenTheFootprintWhenTheObserverStandsOnHighGround()
    {
        SightTerrainGrid grid = NewGrid().Raise(5, 4, CampaignMapGeographyType.Hilly);

        IReadOnlyCollection<Coord> footprint = FieldOfView.GetFootprint(SightTestData.Unit(1, 1, 5, 4, 2), grid[5, 4], grid.Geometry);

        Assert.That(footprint, Has.Count.EqualTo(49), "A Sight of 2 on Hilly ground reaches three rings out.");
    }
}
