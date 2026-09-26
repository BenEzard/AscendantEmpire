using AscendantEmpire.Application.Features.ForceOrders.Movement;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Classes;

/// <summary>
/// One river edge, named by the tile that owns it and which of that tile's two edges it is.
/// </summary>
internal readonly record struct RiverEdgeKey(int X, int Y, RiverEdgeSide Side)
{
    /// <summary>The tile that owns the edge.</summary>
    internal Coord Owner => new(X, Y);

    public override string ToString() => $"({X}, {Y}) {Side}";
}

/// <summary>
/// One edge that breaks an authoring rule, and why.
/// </summary>
/// <remarks>
/// The offending edge is often not one the author just clicked: widening an edge can be refused because of a river further inland, and erasing one
/// because of the stretch it would cut off. Naming the edge that actually breaks the rule is what lets the editor point at it.
/// </remarks>
internal sealed record RiverRuleViolation(RiverEdgeKey Edge, string Reason);

/// <summary>
/// The authoring rules a map's river network has to satisfy, checked over the whole network rather than edge by edge.
/// </summary>
/// <remarks>
/// <para>
/// Rivers run <b>between</b> tiles, and everything here turns on the <b>vertex</b> — the tile corner where edges meet. A tile's South edge runs from
/// vertex <c>(x, y+1)</c> to <c>(x+1, y+1)</c>; its East edge runs from <c>(x+1, y)</c> to <c>(x+1, y+1)</c>. Two edges are <b>connected</b> when they
/// share a vertex, which covers a river running straight on, turning a corner, and a tributary joining a trunk alike.
/// </para>
/// <para>
/// A river reaches the sea at a vertex too, not by having a sea tile beside it. The last stretch of a river running straight into the coast has land on
/// both banks and touches the water only at its end — so a <b>mouth</b> is an edge with a <b>coastal vertex</b>, one where any of the up to four tiles
/// meeting at that corner is water. Defining it by the tiles either side of the edge instead would refuse exactly the rivers that look most like rivers.
/// </para>
/// <para>
/// Two of the rules below are properties of the network, not of an edge, so they cannot be decided while an edge is being drawn: whether an edge
/// reaches the sea depends on every other edge, and which way is seaward depends on where the mouths are. They are therefore checked against the
/// network a write <i>would</i> produce, and the write is refused whole if the answer is no. That also means they are re-checked when an author later
/// changes the width of an edge drawn long ago, which a rule applied only at the moment of drawing would miss.
/// </para>
/// <para>
/// Pure, so the same rules govern every writer — the editor endpoint and the map importer — and so they are testable without a database.
/// See <c>docs/design/campaign-map/tiles.md</c>.
/// </para>
/// </remarks>
internal static class RiverNetworkRules
{
    /// <summary>
    /// Checks a whole river network, returning one violation per offending edge and an empty collection when it is sound.
    /// </summary>
    /// <remarks>
    /// The per-edge rules are checked first and, if any of them fail, returned on their own. An edge with no tile on the other side, or with sea on
    /// both sides, is not an edge a river can be reasoned about at all, so walking the network before those are fixed would only produce a second,
    /// more confusing set of complaints about the same mistake.
    /// </remarks>
    /// <param name="riverEdges">
    /// Every edge on the map carrying a river, and its width. Edges with <see cref="RiverWidth.None"/> must not appear: an absent edge is one with no
    /// river on it.
    /// </param>
    /// <param name="geometry">The map's size and wrapping. Wrapping matters twice: for which tile is on the other side of an edge, and for whether two
    /// edges meeting across the seam share a vertex.</param>
    /// <param name="waterTiles">Every water-Biome tile touching a vertex of any edge in <paramref name="riverEdges"/>. A tile absent from this set is
    /// treated as land, so the caller must cover all of them — <see cref="GetTilesToCheckForWater"/> says which.</param>
    internal static IReadOnlyCollection<RiverRuleViolation> Validate(
        IReadOnlyDictionary<RiverEdgeKey, RiverWidth> riverEdges,
        CampaignMapGeometry geometry,
        IReadOnlySet<Coord> waterTiles)
    {
        ArgumentNullException.ThrowIfNull(riverEdges);
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(waterTiles);

        if (riverEdges.Count == 0)
        {
            return [];
        }

        List<RiverRuleViolation> perEdgeViolations = [];

        foreach (KeyValuePair<RiverEdgeKey, RiverWidth> entry in riverEdges)
        {
            RiverEdgeKey edge = entry.Key;

            if (entry.Value == RiverWidth.None)
            {
                throw new ArgumentException(
                    $"Edge {edge} was passed with a width of None. An edge with no river belongs out of the collection entirely.", nameof(riverEdges));
            }

            if (!TryGetTilesEitherSide(edge, geometry, out Coord owner, out Coord? across))
            {
                perEdgeViolations.Add(new RiverRuleViolation(edge, "That edge faces off the map."));
                continue;
            }

            if (waterTiles.Contains(owner) && waterTiles.Contains(across!))
            {
                perEdgeViolations.Add(new RiverRuleViolation(edge, "That edge has water on both sides."));
            }
        }

        if (perEdgeViolations.Count > 0)
        {
            return perEdgeViolations;
        }

        return ValidateNetwork(riverEdges, geometry, waterTiles);
    }

    /// <summary>
    /// Which tiles a caller has to know the wetness of before it can call <see cref="Validate"/>.
    /// </summary>
    /// <remarks>
    /// Every tile meeting either vertex of every edge, since that is what decides whether the edge reaches the sea, plus the two tiles either side of
    /// it, which the both-water rule needs. Duplicates collapse, so a river of <c>n</c> edges asks about a handful of tiles per edge however large the
    /// map is.
    /// </remarks>
    internal static IReadOnlyCollection<Coord> GetTilesToCheckForWater(IEnumerable<RiverEdgeKey> riverEdges, CampaignMapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(riverEdges);
        ArgumentNullException.ThrowIfNull(geometry);

        HashSet<Coord> needed = [];

        foreach (RiverEdgeKey edge in riverEdges)
        {
            needed.Add(edge.Owner);

            if (TryGetTilesEitherSide(edge, geometry, out _, out Coord? across))
            {
                needed.Add(across!);
            }

            foreach ((int X, int Y) vertex in GetVertices(edge, geometry))
            {
                foreach (Coord tile in GetTilesAtVertex(vertex, geometry))
                {
                    needed.Add(tile);
                }
            }
        }

        return needed;
    }

    /// <summary>
    /// The two rules that need the network as a whole: that every edge reaches the sea, and that no river widens as it runs inland.
    /// </summary>
    private static IReadOnlyCollection<RiverRuleViolation> ValidateNetwork(
        IReadOnlyDictionary<RiverEdgeKey, RiverWidth> riverEdges,
        CampaignMapGeometry geometry,
        IReadOnlySet<Coord> waterTiles)
    {
        Dictionary<(int X, int Y), List<RiverEdgeKey>> edgesByVertex = BuildVertexIndex(riverEdges.Keys, geometry);

        HashSet<RiverEdgeKey> mouths = FindMouths(edgesByVertex, geometry, waterTiles);

        // Hop count from the nearest mouth. This is what "seaward" means: of two connected edges, the one with the smaller distance is the one nearer
        // the sea, whichever order the author happened to draw them in.
        Dictionary<RiverEdgeKey, int> distanceToSea = MeasureDistanceToSea(mouths, edgesByVertex, geometry);

        List<RiverRuleViolation> violations = [];

        // Rule: a river starts at the sea. An edge the walk never reached is a river with no mouth — either floating inland on its own, or a stretch
        // that an erase further down would cut off from the coast.
        foreach (RiverEdgeKey edge in riverEdges.Keys)
        {
            if (!distanceToSea.ContainsKey(edge))
            {
                violations.Add(new RiverRuleViolation(edge, "That edge does not reach the sea."));
            }
        }

        if (violations.Count > 0)
        {
            return violations;
        }

        // Rule: a river never widens inland. Checked at every vertex, because that is where two edges of a river meet; a vertex joins at most four
        // edges, so the pairwise comparison below stays trivial however large the map is.
        foreach (List<RiverEdgeKey> atVertex in edgesByVertex.Values)
        {
            foreach (RiverEdgeKey seaward in atVertex)
            {
                foreach (RiverEdgeKey inland in atVertex)
                {
                    // Only a strictly shorter path to the sea makes one edge the seaward of the other. Two edges the same distance out constrain each
                    // other not at all, which is what lets two arms of a delta carry different widths.
                    if (distanceToSea[seaward] >= distanceToSea[inland])
                    {
                        continue;
                    }

                    if (riverEdges[inland] > riverEdges[seaward])
                    {
                        violations.Add(new RiverRuleViolation(
                            inland,
                            $"A {Describe(riverEdges[inland])} river cannot run inland of a {Describe(riverEdges[seaward])} one."));
                    }
                }
            }
        }

        // One edge can sit inland of two different edges and be refused twice over; the author only needs telling once.
        return [.. violations.DistinctBy(violation => violation.Edge)];
    }

    /// <summary>
    /// The edges that meet the sea: those with a vertex at a tile corner where any of the surrounding tiles is water.
    /// </summary>
    private static HashSet<RiverEdgeKey> FindMouths(
        Dictionary<(int X, int Y), List<RiverEdgeKey>> edgesByVertex,
        CampaignMapGeometry geometry,
        IReadOnlySet<Coord> waterTiles)
    {
        HashSet<RiverEdgeKey> mouths = [];

        foreach (KeyValuePair<(int X, int Y), List<RiverEdgeKey>> entry in edgesByVertex)
        {
            if (!GetTilesAtVertex(entry.Key, geometry).Any(waterTiles.Contains))
            {
                continue;
            }

            foreach (RiverEdgeKey edge in entry.Value)
            {
                mouths.Add(edge);
            }
        }

        return mouths;
    }

    /// <summary>
    /// Groups the edges by the vertices they end at, which is the adjacency the rest of the walk runs on.
    /// </summary>
    private static Dictionary<(int X, int Y), List<RiverEdgeKey>> BuildVertexIndex(IEnumerable<RiverEdgeKey> edges, CampaignMapGeometry geometry)
    {
        Dictionary<(int X, int Y), List<RiverEdgeKey>> edgesByVertex = [];

        foreach (RiverEdgeKey edge in edges)
        {
            foreach ((int X, int Y) vertex in GetVertices(edge, geometry))
            {
                if (!edgesByVertex.TryGetValue(vertex, out List<RiverEdgeKey>? atVertex))
                {
                    atVertex = [];
                    edgesByVertex[vertex] = atVertex;
                }

                atVertex.Add(edge);
            }
        }

        return edgesByVertex;
    }

    /// <summary>
    /// A breadth-first walk out from every mouth at once, giving each edge its hop count to the nearest one.
    /// </summary>
    /// <remarks>
    /// Every mouth is a source, so a network fed by two river mouths is measured from whichever is closer, and an edge that no walk reaches is simply
    /// absent from the result.
    /// </remarks>
    private static Dictionary<RiverEdgeKey, int> MeasureDistanceToSea(
        IReadOnlySet<RiverEdgeKey> mouths,
        Dictionary<(int X, int Y), List<RiverEdgeKey>> edgesByVertex,
        CampaignMapGeometry geometry)
    {
        Dictionary<RiverEdgeKey, int> distance = [];
        Queue<RiverEdgeKey> queue = new();

        foreach (RiverEdgeKey mouth in mouths)
        {
            distance[mouth] = 0;
            queue.Enqueue(mouth);
        }

        while (queue.Count > 0)
        {
            RiverEdgeKey current = queue.Dequeue();
            int nextDistance = distance[current] + 1;

            foreach ((int X, int Y) vertex in GetVertices(current, geometry))
            {
                foreach (RiverEdgeKey neighbour in edgesByVertex[vertex])
                {
                    if (distance.ContainsKey(neighbour))
                    {
                        continue;
                    }

                    distance[neighbour] = nextDistance;
                    queue.Enqueue(neighbour);
                }
            }
        }

        return distance;
    }

    /// <summary>
    /// The two tiles an edge runs between: the tile that owns it, and the tile on the other side.
    /// </summary>
    /// <returns><see langword="false"/> when the edge faces off a non-wrapping edge of the map, in which case there is no tile across it.</returns>
    private static bool TryGetTilesEitherSide(RiverEdgeKey edge, CampaignMapGeometry geometry, out Coord owner, out Coord? across)
    {
        owner = edge.Owner;

        MoveDirection direction = edge.Side == RiverEdgeSide.South ? MoveDirection.South : MoveDirection.East;

        // The same neighbour arithmetic movement and sight use, so a river can never disagree with a step about which tiles are next to each other.
        return MapDirections.TryGetNeighbour(owner, direction, geometry.Width, geometry.Height, geometry.WrapsHorizontally, geometry.WrapsVertically, out across);
    }

    /// <summary>
    /// The two tile corners an edge runs between.
    /// </summary>
    /// <remarks>
    /// Vertices are numbered like tiles but run one further in each direction, so a <c>W × H</c> map has <c>(W+1) × (H+1)</c> of them. On an axis that
    /// wraps, the far vertex is the near one: vertex <c>W</c> and vertex <c>0</c> are the same corner, which is what lets a river cross the seam and
    /// still count as one river.
    /// </remarks>
    private static (int X, int Y)[] GetVertices(RiverEdgeKey edge, CampaignMapGeometry geometry)
    {
        return edge.Side == RiverEdgeSide.South
            ? [Normalise(edge.X, edge.Y + 1, geometry), Normalise(edge.X + 1, edge.Y + 1, geometry)]
            : [Normalise(edge.X + 1, edge.Y, geometry), Normalise(edge.X + 1, edge.Y + 1, geometry)];
    }

    /// <summary>
    /// The up to four tiles that meet at a tile corner.
    /// </summary>
    /// <remarks>
    /// A corner on the outside of a map that does not wrap has fewer than four; those that fall off it are left out rather than clamped onto a tile
    /// that is not really there.
    /// </remarks>
    private static IEnumerable<Coord> GetTilesAtVertex((int X, int Y) vertex, CampaignMapGeometry geometry)
    {
        foreach (int offsetX in (int[])[-1, 0])
        {
            foreach (int offsetY in (int[])[-1, 0])
            {
                if (TryGetTile(vertex.X + offsetX, vertex.Y + offsetY, geometry, out Coord? tile))
                {
                    yield return tile!;
                }
            }
        }
    }

    /// <summary>
    /// Places a tile coordinate on the map, folding it round any axis that wraps.
    /// </summary>
    /// <returns><see langword="false"/> when the coordinate is off a non-wrapping edge, where there is no such tile.</returns>
    private static bool TryGetTile(int x, int y, CampaignMapGeometry geometry, out Coord? tile)
    {
        tile = null;

        if (x < 0 || x >= geometry.Width)
        {
            if (!geometry.WrapsHorizontally)
            {
                return false;
            }

            x = MapDirections.Modulo(x, geometry.Width);
        }

        if (y < 0 || y >= geometry.Height)
        {
            if (!geometry.WrapsVertically)
            {
                return false;
            }

            y = MapDirections.Modulo(y, geometry.Height);
        }

        tile = new Coord(x, y);
        return true;
    }

    /// <summary>
    /// Folds a vertex back onto the map on any axis that wraps, so the two sides of a seam name the same corner.
    /// </summary>
    private static (int X, int Y) Normalise(int vertexX, int vertexY, CampaignMapGeometry geometry)
    {
        return (
            geometry.WrapsHorizontally ? MapDirections.Modulo(vertexX, geometry.Width) : vertexX,
            geometry.WrapsVertically ? MapDirections.Modulo(vertexY, geometry.Height) : vertexY);
    }

    /// <summary>Names a width for the author, lower case because it always appears mid-sentence.</summary>
    private static string Describe(RiverWidth width) => width.ToString().ToLowerInvariant();
}
