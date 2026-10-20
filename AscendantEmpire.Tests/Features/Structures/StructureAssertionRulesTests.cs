using AscendantEmpire.Application.Features.ForceOrders.Movement;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Structures.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;

namespace AscendantEmpire.Tests.Features.Structures;

/// <summary>
/// Pure unit tests for <see cref="StructureAssertionRules"/> — no database, because a territory radius is arithmetic over the map's shape.
/// </summary>
/// <remarks>
/// The important one here is the equivalence with <see cref="MapDirections.GetNeighbours"/>. A radius of 1 has to be exactly the tile and its eight neighbours, or a
/// village's territory and the adjacency every other rule uses would quietly disagree.
/// </remarks>
[TestFixture]
internal class StructureAssertionRulesTests
{
    private static CampaignMapGeometry NewGeometry(bool wrapsHorizontally = false)
    {
        return new CampaignMapGeometry(CampaignMapOffsetId: 0, Width: 10, Height: 8, WrapsHorizontally: wrapsHorizontally, WrapsVertically: false);
    }

    [Test, Description("A radius of nothing is the ground the Structure stands on, which is the camp and every Structure that is nobody's Settlement.")]
    public void ShouldAssertOverOneTileAtRadiusZero()
    {
        IReadOnlyCollection<Coord> tiles = StructureAssertionRules.GetAssertedTiles(new Coord(5, 4), territoryRadius: 0, NewGeometry());

        Assert.That(tiles, Is.EquivalentTo(new[] { new Coord(5, 4) }));
    }

    [Test, Description("A village's radius of one is the three-by-three block around it, so it owns nine tiles rather than the four an orthogonal reading would give.")]
    public void ShouldAssertOverNineTilesAtRadiusOne()
    {
        IReadOnlyCollection<Coord> tiles = StructureAssertionRules.GetAssertedTiles(new Coord(5, 4), territoryRadius: 1, NewGeometry());

        Assert.That(tiles, Has.Count.EqualTo(9));
    }

    [Test, Description("A radius of one must be exactly the tile plus the neighbours movement and enclosure use, or a village's territory would disagree with every other rule.")]
    public void ShouldMatchTheEightWayNeighbourRingAtRadiusOne()
    {
        CampaignMapGeometry geometry = NewGeometry();
        Coord origin = new(5, 4);

        HashSet<(int X, int Y)> asserted = [.. StructureAssertionRules.GetAssertedTiles(origin, territoryRadius: 1, geometry).Select(coord => (coord.X, coord.Y))];

        HashSet<(int X, int Y)> expected = [.. MapDirections
            .GetNeighbours(origin, geometry.Width, geometry.Height, geometry.WrapsHorizontally, geometry.WrapsVertically)
            .Select(coord => (coord.X, coord.Y))
            .Append((origin.X, origin.Y))];

        Assert.That(asserted, Is.EquivalentTo(expected));
    }

    [Test, Description("A village on the first column of a wrapping map reaches round the seam, because the radius uses the same wrap arithmetic a step does.")]
    public void ShouldReachAcrossAWrappingSeam()
    {
        IReadOnlyCollection<Coord> tiles = StructureAssertionRules.GetAssertedTiles(new Coord(0, 4), territoryRadius: 1, NewGeometry(wrapsHorizontally: true));

        Assert.Multiple(() =>
        {
            Assert.That(tiles, Has.Count.EqualTo(9));
            Assert.That(tiles.Any(coord => coord.X == 9), Is.True, "The far column is one step west of column 0.");
        });
    }

    [Test, Description("A village in a corner of a map that does not wrap simply gets a smaller territory, rather than the founding being refused for reasons the map does not show.")]
    public void ShouldGiveACornerVillageASmallerTerritory()
    {
        IReadOnlyCollection<Coord> tiles = StructureAssertionRules.GetAssertedTiles(new Coord(0, 0), territoryRadius: 1, NewGeometry());

        Assert.That(tiles, Has.Count.EqualTo(4));
    }
}
