using AscendantEmpire.Application.Features.ForceOrders.Movement;
using AscendantEmpire.BackendContracts.CampaignMaps;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

/// <summary>
/// One tile in an Observer's sight tree, and the tiles whose line of sight runs back through it.
/// </summary>
/// <remarks>
/// A tile is only ever reached through its parent, so anything that blocks the view at a node hides every descendant of it. See <see cref="SightRadius"/>.
/// </remarks>
internal sealed class SightNode(int x, int y)
{
    internal int X { get; } = x;

    internal int Y { get; } = y;

    /// <summary>
    /// The tiles one step further out whose line of sight back to the Observer passes through this one.
    /// </summary>
    internal List<SightNode> Children { get; } = [];
}

/// <summary>
/// Works out which tiles lie within an Observer's sight range, and the fixed path each one is seen along.
/// </summary>
/// <remarks>
/// <para>
/// Pure geometry: no terrain and no database. Lifted out of the old <c>GetCampaignMapTileRadiusHandler</c>, which did the same arithmetic behind a mediator
/// call it only needed for the map's dimensions.
/// </para>
/// <para>
/// The range is a square — Chebyshev distance, so a diagonal step counts as one. Each tile's <b>sight parent</b> is the tile one step back towards the
/// Observer on each axis at once, which walks a tile diagonally until it is level with the Observer on one axis and then straight in. That is the
/// "diagonal for as long as possible" path in <c>docs/design/campaign-map.md</c>, and it is a fixed tree rather than true ray-casting: each tile has
/// exactly one parent, and whatever blocks that parent blocks it.
/// </para>
/// <para>
/// Wrapping is honoured on each axis independently. A map smaller than the square on a wrapping axis folds tiles onto one another; each is kept once, so
/// the tree stays a tree.
/// </para>
/// </remarks>
internal static class SightRadius
{
    /// <summary>
    /// Builds the sight tree for an Observer at <paramref name="origin"/>, returning the tiles adjacent to it with everything further out hanging off them.
    /// </summary>
    /// <remarks>
    /// The Observer's own tile is not in the result: it is always seen, and is where every walk starts from. A range of zero or less has no tree at all.
    /// </remarks>
    /// <param name="sightRange">The range already resolved against the ground the Observer stands on — see <see cref="ObserverSightRules"/>.</param>
    internal static IReadOnlyCollection<SightNode> BuildSightTree(Coord origin, int sightRange, CampaignMapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(geometry);

        if (sightRange <= 0)
        {
            return [];
        }

        (int X, int Y) originKey = (origin.X, origin.Y);

        // One node per distinct tile. A small wrapping map can reach the same tile from two sides of the square, and it must still be one node.
        Dictionary<(int X, int Y), SightNode> nodesByCoord = new() { [originKey] = new SightNode(origin.X, origin.Y) };

        // Child -> parent links, kept in the order they were found so the tree - and so the order tiles are reported in - is deterministic. The set stops a
        // folded tile being linked to the same parent twice.
        List<((int X, int Y) Child, (int X, int Y) Parent)> links = [];
        HashSet<((int X, int Y) Child, (int X, int Y) Parent)> linkSet = [];

        for (int y = origin.Y - sightRange; y <= origin.Y + sightRange; y++)
        {
            for (int x = origin.X - sightRange; x <= origin.X + sightRange; x++)
            {
                if (TryPlaceOnMap(x, y, geometry, out int mapX, out int mapY) == false)
                {
                    continue;
                }

                (int X, int Y) childKey = (mapX, mapY);

                // The Observer's own tile, or a tile that wrapped all the way back onto it. Either way it has no parent: it is the root.
                if (childKey == originKey)
                {
                    continue;
                }

                GetOrAddNode(nodesByCoord, childKey);

                // Step one tile back towards the Observer on each axis at once. The delta is measured on the map, the shorter way round on an axis that wraps,
                // so a tile just across a wrapped edge points back across it rather than the long way round the world.
                int parentX = mapX - Math.Sign(MapDirections.GetAxisDelta(origin.X, mapX, geometry.Width, geometry.WrapsHorizontally));
                int parentY = mapY - Math.Sign(MapDirections.GetAxisDelta(origin.Y, mapY, geometry.Height, geometry.WrapsVertically));

                if (geometry.WrapsHorizontally)
                {
                    parentX = MapDirections.Modulo(parentX, geometry.Width);
                }

                if (geometry.WrapsVertically)
                {
                    parentY = MapDirections.Modulo(parentY, geometry.Height);
                }

                (int X, int Y) parentKey = (parentX, parentY);

                // Only possible on a map too small to be real, where the step back folds onto the tile itself. A self-link would be a cycle, so drop it.
                if (parentKey == childKey)
                {
                    continue;
                }

                GetOrAddNode(nodesByCoord, parentKey);

                if (linkSet.Add((childKey, parentKey)))
                {
                    links.Add((childKey, parentKey));
                }
            }
        }

        foreach (((int X, int Y) child, (int X, int Y) parent) in links)
        {
            nodesByCoord[parent].Children.Add(nodesByCoord[child]);
        }

        return nodesByCoord[originKey].Children;
    }

    /// <summary>
    /// Every tile within <paramref name="radius"/> of <paramref name="origin"/>, the origin included, wrapped or clipped to the map and each listed once.
    /// </summary>
    /// <remarks>
    /// Used wherever only the <i>area</i> matters and not the sight paths through it: the terrain an Observer's walk might need, and the box of ground to
    /// search for Observers around a tile. A negative radius is empty; a radius of zero is the origin alone.
    /// </remarks>
    internal static IReadOnlyCollection<Coord> GetSquare(Coord origin, int radius, CampaignMapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(geometry);

        if (radius < 0)
        {
            return [];
        }

        List<Coord> square = [];
        HashSet<(int X, int Y)> seen = [];

        for (int y = origin.Y - radius; y <= origin.Y + radius; y++)
        {
            for (int x = origin.X - radius; x <= origin.X + radius; x++)
            {
                if (TryPlaceOnMap(x, y, geometry, out int mapX, out int mapY) && seen.Add((mapX, mapY)))
                {
                    square.Add(new Coord(mapX, mapY));
                }
            }
        }

        return square;
    }

    /// <summary>
    /// Wraps a raw square coordinate onto the map, or reports that it falls off an edge that does not wrap.
    /// </summary>
    private static bool TryPlaceOnMap(int x, int y, CampaignMapGeometry geometry, out int mapX, out int mapY)
    {
        mapX = geometry.WrapsHorizontally ? MapDirections.Modulo(x, geometry.Width) : x;
        mapY = geometry.WrapsVertically ? MapDirections.Modulo(y, geometry.Height) : y;

        return mapX >= 0 && mapX < geometry.Width && mapY >= 0 && mapY < geometry.Height;
    }

    private static SightNode GetOrAddNode(Dictionary<(int X, int Y), SightNode> nodesByCoord, (int X, int Y) key)
    {
        if (nodesByCoord.TryGetValue(key, out SightNode? node) == false)
        {
            node = new SightNode(key.X, key.Y);
            nodesByCoord[key] = node;
        }

        return node;
    }
}
