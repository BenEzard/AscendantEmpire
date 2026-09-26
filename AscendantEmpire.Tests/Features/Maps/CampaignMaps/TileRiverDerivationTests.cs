using AscendantEmpire.Application.Features.ForceOrders.Movement;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Maps.CampaignMaps;

/// <summary>
/// Tests for <see cref="TileRiverDerivation"/>: turning the two river columns a tile owns into the four edges the client is told about.
/// </summary>
/// <remarks>
/// <para>
/// No database. This is pure arithmetic over a dictionary of columns and the map's wrapping, which is exactly the part worth pinning: a tile's North
/// edge belongs to the tile above it and its West edge to the tile on its left, so getting the ownership or the wrap wrong draws rivers along the
/// wrong borders.
/// </para>
/// <para>
/// The map is 4 x 3 unless a test says otherwise, so there is room for an interior tile with a whole ring around it and edges that face off the map.
/// </para>
/// </remarks>
[TestFixture]
internal class TileRiverDerivationTests
{
    private const int _MAP_WIDTH = 4;
    private const int _MAP_HEIGHT = 3;

    /// <summary>A map whose edges do not join, so the top row's North edges and the left column's West edges face off it.</summary>
    private static CampaignMapGeometry NoWrapping()
    {
        return new CampaignMapGeometry(CampaignMapOffsetId: 100, _MAP_WIDTH, _MAP_HEIGHT, WrapsHorizontally: false, WrapsVertically: false);
    }

    /// <summary>A map whose east and west edges join, and whose north and south edges join.</summary>
    private static CampaignMapGeometry BothAxesWrap()
    {
        return new CampaignMapGeometry(CampaignMapOffsetId: 100, _MAP_WIDTH, _MAP_HEIGHT, WrapsHorizontally: true, WrapsVertically: true);
    }

    /// <summary>The owned columns of one tile, as they are stored: its South and East edges and nothing else.</summary>
    private static KeyValuePair<Coord, TileRiverEdges> Tile(int x, int y, RiverWidth south = RiverWidth.None, RiverWidth east = RiverWidth.None)
    {
        return new KeyValuePair<Coord, TileRiverEdges>(new Coord(x, y), new TileRiverEdges(south, east));
    }

    private static Dictionary<Coord, TileRiverEdges> Map(params KeyValuePair<Coord, TileRiverEdges>[] tiles)
    {
        return new Dictionary<Coord, TileRiverEdges>(tiles);
    }

    [Test, Description("A tile reports its own SouthRiver and EastRiver columns as its South and East edges, unchanged.")]
    public void OwnColumnsBecomeTheSouthAndEastEdges()
    {
        Dictionary<Coord, TileRiverEdges> edges = Map(Tile(1, 1, south: RiverWidth.Medium, east: RiverWidth.Large));

        TileRivers rivers = TileRiverDerivation.Derive(new Coord(1, 1), edges, NoWrapping());

        Assert.Multiple(() =>
        {
            Assert.That(rivers.South, Is.EqualTo(RiverWidth.Medium));
            Assert.That(rivers.East, Is.EqualTo(RiverWidth.Large));
        });
    }

    [Test, Description("A tile's North edge is read off the SouthRiver of the tile above it, which is the row that owns that edge.")]
    public void NorthEdgeComesFromTheTileAbove()
    {
        Dictionary<Coord, TileRiverEdges> edges = Map(Tile(1, 1), Tile(1, 0, south: RiverWidth.Large));

        TileRivers rivers = TileRiverDerivation.Derive(new Coord(1, 1), edges, NoWrapping());

        Assert.That(rivers.North, Is.EqualTo(RiverWidth.Large));
    }

    [Test, Description("A tile's West edge is read off the EastRiver of the tile to its left, which is the row that owns that edge.")]
    public void WestEdgeComesFromTheTileToTheLeft()
    {
        Dictionary<Coord, TileRiverEdges> edges = Map(Tile(1, 1), Tile(0, 1, east: RiverWidth.Medium));

        TileRivers rivers = TileRiverDerivation.Derive(new Coord(1, 1), edges, NoWrapping());

        Assert.That(rivers.West, Is.EqualTo(RiverWidth.Medium));
    }

    [Test, Description("A tile's own South and East columns are never mistaken for its North and West edges, so a single river is reported on one side only.")]
    public void OwnColumnsAreNotReportedOnTheOppositeEdges()
    {
        Dictionary<Coord, TileRiverEdges> edges = Map(Tile(1, 1, south: RiverWidth.Large, east: RiverWidth.Large));

        TileRivers rivers = TileRiverDerivation.Derive(new Coord(1, 1), edges, NoWrapping());

        Assert.Multiple(() =>
        {
            Assert.That(rivers.North, Is.EqualTo(RiverWidth.None));
            Assert.That(rivers.West, Is.EqualTo(RiverWidth.None));
        });
    }

    [Test, Description("Both banks of a river describe the same edge, so the tile above a river reports it South and the tile below reports it North.")]
    public void BothTilesEitherSideOfAnEdgeReportIt()
    {
        Dictionary<Coord, TileRiverEdges> edges = Map(Tile(1, 0, south: RiverWidth.Medium), Tile(1, 1));
        CampaignMapGeometry geometry = NoWrapping();

        TileRivers above = TileRiverDerivation.Derive(new Coord(1, 0), edges, geometry);
        TileRivers below = TileRiverDerivation.Derive(new Coord(1, 1), edges, geometry);

        Assert.Multiple(() =>
        {
            Assert.That(above.South, Is.EqualTo(RiverWidth.Medium));
            Assert.That(below.North, Is.EqualTo(RiverWidth.Medium));
        });
    }

    [Test, Description("An edge is reported even when the tile on the other side of it was never asked about, which is what makes a river visible from one explored bank.")]
    public void AnEdgeIsReportedWhenOnlyOneSideIsBeingDescribed()
    {
        // Only the tile below the river is described; the tile above it - which owns the edge - is loaded but not asked about.
        Dictionary<Coord, TileRiverEdges> edges = Map(Tile(1, 0, south: RiverWidth.Large), Tile(1, 1));

        TileRivers rivers = TileRiverDerivation.Derive(new Coord(1, 1), edges, NoWrapping());

        Assert.That(rivers.North, Is.EqualTo(RiverWidth.Large));
    }

    [Test, Description("On an axis that does not wrap, the top row's North edge and the left column's West edge face off the map and read as None.")]
    public void EdgesFacingOffANonWrappingMapHaveNoRiver()
    {
        // The far row and column carry rivers that would be picked up if the wrap were honoured on a map that does not wrap.
        Dictionary<Coord, TileRiverEdges> edges = Map(
            Tile(0, 0),
            Tile(0, _MAP_HEIGHT - 1, south: RiverWidth.Large),
            Tile(_MAP_WIDTH - 1, 0, east: RiverWidth.Large));

        TileRivers rivers = TileRiverDerivation.Derive(new Coord(0, 0), edges, NoWrapping());

        Assert.Multiple(() =>
        {
            Assert.That(rivers.North, Is.EqualTo(RiverWidth.None));
            Assert.That(rivers.West, Is.EqualTo(RiverWidth.None));
        });
    }

    [Test, Description("On a wrapping axis the first row's North edge is the last row's SouthRiver, and the first column's West edge the last column's EastRiver.")]
    public void EdgesWrapRoundToTheFarRowAndColumn()
    {
        Dictionary<Coord, TileRiverEdges> edges = Map(
            Tile(0, 0),
            Tile(0, _MAP_HEIGHT - 1, south: RiverWidth.Medium),
            Tile(_MAP_WIDTH - 1, 0, east: RiverWidth.Large));

        TileRivers rivers = TileRiverDerivation.Derive(new Coord(0, 0), edges, BothAxesWrap());

        Assert.Multiple(() =>
        {
            Assert.That(rivers.North, Is.EqualTo(RiverWidth.Medium));
            Assert.That(rivers.West, Is.EqualTo(RiverWidth.Large));
        });
    }

    [Test, Description("A tile whose neighbours' columns were not loaded reports no river on the edges those neighbours own, rather than failing.")]
    public void MissingNeighbourColumnsReadAsNoRiver()
    {
        Dictionary<Coord, TileRiverEdges> edges = Map(Tile(2, 2, south: RiverWidth.Medium));

        TileRivers rivers = TileRiverDerivation.Derive(new Coord(2, 2), edges, NoWrapping());

        Assert.Multiple(() =>
        {
            Assert.That(rivers.South, Is.EqualTo(RiverWidth.Medium));
            Assert.That(rivers.North, Is.EqualTo(RiverWidth.None));
            Assert.That(rivers.West, Is.EqualTo(RiverWidth.None));
        });
    }

    [Test, Description("Describing an interior tile loads its own columns plus the two neighbours that own its North and West edges, and nothing else.")]
    public void OnlyTheTwoOwningNeighboursAreLoaded()
    {
        IReadOnlyCollection<Coord> needed = TileRiverDerivation.GetCoordsToLoad([new Coord(2, 2)], NoWrapping());

        Assert.That(needed, Is.EquivalentTo(new[] { new Coord(2, 2), new Coord(2, 1), new Coord(1, 2) }));
    }

    [Test, Description("A block of adjacent tiles shares most of its owning neighbours, so the set to load collapses rather than growing threefold.")]
    public void NeighboursSharedByABlockAreLoadedOnce()
    {
        Coord[] block = [new Coord(1, 1), new Coord(2, 1), new Coord(1, 2), new Coord(2, 2)];

        IReadOnlyCollection<Coord> needed = TileRiverDerivation.GetCoordsToLoad(block, NoWrapping());

        // The block itself, plus the row above it and the column to its left: nine tiles rather than twelve.
        Assert.That(needed, Is.EquivalentTo(new[]
        {
            new Coord(1, 1), new Coord(2, 1), new Coord(1, 2), new Coord(2, 2),
            new Coord(1, 0), new Coord(2, 0),
            new Coord(0, 1), new Coord(0, 2)
        }));
    }

    [Test, Description("On a map that does not wrap, a tile on the top-left corner asks for no neighbour off the map.")]
    public void NoNeighbourOffANonWrappingMapIsLoaded()
    {
        IReadOnlyCollection<Coord> needed = TileRiverDerivation.GetCoordsToLoad([new Coord(0, 0)], NoWrapping());

        Assert.That(needed, Is.EquivalentTo(new[] { new Coord(0, 0) }));
    }
}
