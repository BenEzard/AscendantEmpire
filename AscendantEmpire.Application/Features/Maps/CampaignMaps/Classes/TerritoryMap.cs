using AscendantEmpire.Application.Features.ForceOrders.Movement;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

/// <summary>
/// One Game's campaign map as the territory pass sees it: every tile, keyed by coordinate, with the geometry it sits in.
/// </summary>
/// <remarks>
/// <para>
/// The in-memory bitmap <c>docs/design/campaign-map/tile-ownership.md</c> calls for, in the shape <see cref="FieldOfView"/> established - a coordinate-keyed dictionary of
/// flat records rather than a literal bit array. <see cref="FieldOfView"/> takes its dictionary and its geometry as two loose arguments; this pass needs the same tiles in
/// two different shapes, so they are bundled here and the second is <i>derived</i> from the first rather than passed in beside it, which is what stops the two disagreeing.
/// </para>
/// <para>
/// Immutable. What the pass decides goes into a <see cref="TerritoryWorkingSet"/> built on top of this, and the deltas are the difference between the two.
/// </para>
/// </remarks>
internal sealed class TerritoryMap
{
    /// <summary>
    /// The narrowest river that seals an enclosure boundary.
    /// </summary>
    /// <remarks>
    /// A river is a natural boundary for an area to claim, so Medium and Large both seal and only a trickle does not. Written as a threshold rather than as
    /// <c>!= None</c> because <see cref="RiverWidth"/> is ordered by width and may gain a narrower member: the rule is "Medium or wider", not "any river at all".
    /// </remarks>
    internal const RiverWidth SEALING_RIVER_WIDTH = RiverWidth.Medium;

    /// <summary>The river columns in the shape <see cref="RiverEdges.Create"/> asks for, built once per pass rather than once per visited tile.</summary>
    private readonly IReadOnlyDictionary<Coord, TileRiverEdges> _riverEdgesByCoord;

    private TerritoryMap(CampaignMapGeometry geometry, IReadOnlyDictionary<(int X, int Y), TerritoryTile> tilesByCoord, IReadOnlyDictionary<Coord, TileRiverEdges> riverEdgesByCoord)
    {
        Geometry = geometry;
        TilesByCoord = tilesByCoord;
        _riverEdgesByCoord = riverEdgesByCoord;
    }

    internal CampaignMapGeometry Geometry { get; }

    /// <summary>Every tile of the Game, keyed by coordinate.</summary>
    internal IReadOnlyDictionary<(int X, int Y), TerritoryTile> TilesByCoord { get; }

    /// <summary>
    /// Builds the map from the tiles the reader loaded.
    /// </summary>
    /// <remarks>A duplicate coordinate is a defect in the reader rather than a state of the map, so the last one wins and nothing is thrown here.</remarks>
    internal static TerritoryMap Create(CampaignMapGeometry geometry, IEnumerable<TerritoryTile> tiles)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(tiles);

        Dictionary<(int X, int Y), TerritoryTile> tilesByCoord = [];
        Dictionary<Coord, TileRiverEdges> riverEdgesByCoord = [];

        foreach (TerritoryTile tile in tiles)
        {
            tilesByCoord[tile.Key] = tile;

            // Every tile goes in, including the riverless ones. RiverEdges reads an absent cell as off the map and gives None for any edge touching it, so leaving out
            // the tiles with no river of their own would make a river look as though it ended wherever its far bank happened to be dry.
            riverEdgesByCoord[tile.Location] = tile.Rivers;
        }

        return new TerritoryMap(geometry, tilesByCoord, riverEdgesByCoord);
    }

    /// <summary>The tile at a coordinate, or <see langword="null"/> where the Game has no such tile.</summary>
    internal TerritoryTile? Get((int X, int Y) coord)
    {
        return TilesByCoord.GetValueOrDefault(coord);
    }

    /// <summary>
    /// Every tile adjacent to <paramref name="coord"/> - the eight-way ring, honouring the map's wrapping.
    /// </summary>
    /// <remarks>
    /// Through <see cref="MapDirections.GetNeighbours"/>, so enclosure is tested on the same adjacency movement uses and a ring the rule calls closed cannot be walked
    /// through at a corner. Directions running off a non-wrapping edge are simply absent, which is why the map edge needs no special case anywhere in the pass.
    /// </remarks>
    internal IReadOnlyCollection<Coord> GetNeighbours(Coord coord)
    {
        return MapDirections.GetNeighbours(coord, Geometry.Width, Geometry.Height, Geometry.WrapsHorizontally, Geometry.WrapsVertically);
    }

    /// <summary>
    /// Whether a river seals the step from <paramref name="from"/> one tile in <paramref name="direction"/>, so an enclosure region cannot leak across it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately a thin wrapper over <see cref="RiverMovementRules.Evaluate"/> rather than its own geometry. <c>Crossed</c> already is the shared edge for an orthogonal
    /// step, and for a diagonal it already is "sealed only when <b>both</b> ways round the corner cross a river, at the easier route's width" - so a river that merely
    /// ends at a corner does not seal it, and the seal agrees exactly with what a Force stepping across that corner would pay.
    /// </para>
    /// <para>
    /// <c>Along</c> and <c>TargetBank</c> are computed and ignored. That is a trivial cost next to having one implementation of the corner rule.
    /// </para>
    /// </remarks>
    internal bool IsSealedByRiver(Coord from, MoveDirection direction)
    {
        ArgumentNullException.ThrowIfNull(from);

        RiverEdges edges = RiverEdges.Create(from, _riverEdgesByCoord, Geometry.Width, Geometry.Height, Geometry.WrapsHorizontally, Geometry.WrapsVertically);

        return RiverMovementRules.Evaluate(edges, direction).Crossed >= SEALING_RIVER_WIDTH;
    }

    /// <summary>
    /// Whether a region occupying these columns and rows is <b>bounded</b> - genuinely surrounded, rather than running away to the limits of the world.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Enclosure means a Player encircled some ground. Having no open boundary is not the same thing, and on a map that wraps the difference is the whole game: the
    /// complement of a small blob of territory on a torus is the rest of the world, which has no open boundary anywhere, so a single village would ring the planet.
    /// </para>
    /// <para>
    /// A region fails to be bounded in one of two ways, which are the same fact on the two kinds of axis:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///     On an axis that <b>does not wrap</b>, it reaches the map's edge. The edge of the world is not anybody's territory, so leaning a ring against it encircles
    ///     nothing.
    ///   </description></item>
    ///   <item><description>
    ///     On an axis that <b>wraps</b>, it occupies every column (or row) on that axis, which means it has met itself the long way round. That is the torus's version
    ///     of running off the edge.
    ///   </description></item>
    /// </list>
    /// <para>
    /// The price is the pocket genuinely held against the world's edge, or one that genuinely girdles a wrapping world, neither of which is absorbed. That is the right
    /// way round: the legitimate case and the pathological one are the same shape, and only their size tells them apart, so the rule refuses both rather than acquiring
    /// the region size cap the design has deliberately done without.
    /// </para>
    /// </remarks>
    /// <param name="columns">Every distinct X the region occupies.</param>
    /// <param name="rows">Every distinct Y the region occupies.</param>
    internal bool IsBounded(IReadOnlyCollection<int> columns, IReadOnlyCollection<int> rows)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);

        bool boundedHorizontally = Geometry.WrapsHorizontally
            ? columns.Count < Geometry.Width
            : columns.Contains(0) == false && columns.Contains(Geometry.Width - 1) == false;

        bool boundedVertically = Geometry.WrapsVertically
            ? rows.Count < Geometry.Height
            : rows.Contains(0) == false && rows.Contains(Geometry.Height - 1) == false;

        return boundedHorizontally && boundedVertically;
    }

    /// <summary>
    /// Every neighbour of <paramref name="coord"/> that a river does not seal the way to.
    /// </summary>
    /// <remarks>
    /// <b>A sealing river edge removes the adjacency entirely.</b> The tile across it is neither a region member nor a boundary tile, which is what makes a river a real
    /// boundary: a rival on the far bank of a sealing river never vetoes an absorption, because as far as the rule is concerned they are not on the region's edge at all.
    /// </remarks>
    internal IReadOnlyCollection<Coord> GetUnsealedNeighbours(Coord coord)
    {
        ArgumentNullException.ThrowIfNull(coord);

        // One RiverEdges for the whole ring rather than one per direction: building it is the expensive half, and it already holds every edge in the 3x3 block.
        RiverEdges edges = RiverEdges.Create(coord, _riverEdgesByCoord, Geometry.Width, Geometry.Height, Geometry.WrapsHorizontally, Geometry.WrapsVertically);

        List<Coord> unsealed = [];

        foreach (MoveDirection direction in Enum.GetValues<MoveDirection>())
        {
            if (MapDirections.TryGetNeighbour(coord, direction, Geometry.Width, Geometry.Height, Geometry.WrapsHorizontally, Geometry.WrapsVertically, out Coord? neighbour) == false)
            {
                continue;
            }

            if (RiverMovementRules.Evaluate(edges, direction).Crossed >= SEALING_RIVER_WIDTH)
            {
                continue;
            }

            unsealed.Add(neighbour!);
        }

        // A map that wraps on a one- or two-tile axis folds directions onto the same tile, exactly as GetNeighbours has to allow for.
        return [.. unsealed.Distinct()];
    }
}
