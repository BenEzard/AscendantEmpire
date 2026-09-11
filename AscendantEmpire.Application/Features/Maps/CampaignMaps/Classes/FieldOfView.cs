using AscendantEmpire.Application.Features.ForceOrders.Movement;
using AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;
using AscendantEmpire.BackendContracts.CampaignMaps;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

/// <summary>
/// The sight rules: given an Observer and the terrain around it, which tiles it can see.
/// </summary>
/// <remarks>
/// <para>
/// This is the one place a field of view is computed. The live map read (<c>GetPlayerCampaignMapHandler</c>, through <c>IFieldOfViewService</c>) and the
/// intelligence witness service both come here, so live vision and intelligence cannot drift apart. It was previously written out inside the map read,
/// tangled with the database queries, the response projection and the exploration stamp.
/// </para>
/// <para>
/// Pure: no database, no side effects, testable without a container. The caller loads the terrain; <see cref="GetFootprint"/> says which terrain that is.
/// </para>
/// <para>
/// The rules, in the order they apply — see "Vision on the campaign map" in <c>docs/design/campaign-map.md</c>:
/// <list type="number">
///   <item><description>A negative Sight is no Observer at all. Otherwise the occupied tile is always seen.</description></item>
///   <item><description>The occupied tile adjusts the range: a blocking Biome zeroes it, high ground adds to it (<see cref="ObserverSightRules"/>).</description></item>
///   <item><description>The range is walked out along the sight tree (<see cref="SightRadius"/>). A tile is seen when its branch reaches it; a blocking Biome, or ground higher than the tile before it, is seen and stops that branch.</description></item>
///   <item><description>An Observer blinded by its own tile also sees the adjacent tiles its movement domain could never enter (<see cref="BlockedSightReveal"/>).</description></item>
/// </list>
/// </para>
/// </remarks>
internal static class FieldOfView
{
    /// <summary>
    /// How far the Observer actually sees once the ground it stands on is taken into account.
    /// </summary>
    internal static int ResolveSightRange(SightObserver observer, SightTerrain occupiedTile)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(occupiedTile);

        return ObserverSightRules.ResolveSightRange(
            baseSightRange: observer.BaseSightRange,
            occupiedTileBlocksSight: occupiedTile.DoesBlockSight,
            occupiedTileRelativeElevation: occupiedTile.RelativeElevation);
    }

    /// <summary>
    /// Every tile whose terrain <see cref="ComputeSeenTiles"/> could need for this Observer, once its occupied tile is known.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Loading terrain is therefore two passes: the occupied tiles first, because they decide each range, then the footprints. The first pass is tiny, and
    /// it keeps the second from loading a blinded Observer's whole square for nothing.
    /// </para>
    /// <para>
    /// The square of the resolved range, plus the ring of neighbours when the Observer is blinded. A tile missing from the loaded terrain is simply never
    /// seen, so the footprint is a ceiling, not a promise.
    /// </para>
    /// </remarks>
    internal static IReadOnlyCollection<Coord> GetFootprint(SightObserver observer, SightTerrain occupiedTile, CampaignMapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(occupiedTile);
        ArgumentNullException.ThrowIfNull(geometry);

        if (observer.BaseSightRange < 0)
        {
            return [];
        }

        List<Coord> footprint = [.. SightRadius.GetSquare(observer.Location, ResolveSightRange(observer, occupiedTile), geometry)];

        if (occupiedTile.DoesBlockSight)
        {
            footprint.AddRange(GetNeighbours(observer.Location, geometry));
        }

        return [.. footprint.Distinct()];
    }

    /// <summary>
    /// The tiles the Observer can see, its own tile first and then in the order the view reaches them.
    /// </summary>
    /// <param name="terrainByCoord">World-map terrain keyed by coordinate, holding at least the Observer's <see cref="GetFootprint"/>.</param>
    /// <param name="movementDomainStrategies">Every registered movement domain strategy, which the blinded reveal defers to.</param>
    /// <returns>
    /// Empty when the Observer has a negative Sight, or when its own tile is missing from <paramref name="terrainByCoord"/> — an Observer standing nowhere
    /// the caller could read sees nothing, rather than failing the whole read.
    /// </returns>
    internal static IReadOnlyCollection<SightTerrain> ComputeSeenTiles(
        SightObserver observer,
        IReadOnlyDictionary<(int X, int Y), SightTerrain> terrainByCoord,
        CampaignMapGeometry geometry,
        IEnumerable<IMovementDomainStrategy> movementDomainStrategies)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(terrainByCoord);
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(movementDomainStrategies);

        if (observer.BaseSightRange < 0)
        {
            return [];
        }

        if (terrainByCoord.TryGetValue((observer.Location.X, observer.Location.Y), out SightTerrain? occupiedTile) == false)
        {
            return [];
        }

        // The occupied tile is always seen, and goes in the seen set straight away so a small wrapping map that walks back round onto it cannot report it
        // a second time.
        List<SightTerrain> seenTiles = [occupiedTile];
        HashSet<(int X, int Y)> seenCoords = [(occupiedTile.X, occupiedTile.Y)];

        int sightRange = ResolveSightRange(observer, occupiedTile);

        if (sightRange > 0)
        {
            // Shared across every branch of this one Observer's walk, so a tile reachable along two branches of a folded map is walked once.
            HashSet<(int X, int Y)> visitedCoords = [];

            foreach (SightNode firstStep in SightRadius.BuildSightTree(observer.Location, sightRange, geometry))
            {
                WalkSightBranch(firstStep, occupiedTile, sightRange, terrainByCoord, seenTiles, seenCoords, visitedCoords);
            }
        }

        // Dense terrain leaves the Observer seeing only its own tile, which would make ground it cannot walk to permanently unknowable. This adds back
        // exactly that ground and nothing else.
        if (occupiedTile.DoesBlockSight)
        {
            RevealGroundTheObserverCannotReach(observer, geometry, terrainByCoord, movementDomainStrategies, seenTiles, seenCoords);
        }

        return seenTiles;
    }

    /// <summary>
    /// Follows one branch of the sight tree outwards, marking each tile seen until something blocks the view or the range runs out.
    /// </summary>
    /// <param name="parentTile">The tile the view reaches this one from, which its elevation is compared against.</param>
    /// <param name="remainingRange">How many more tiles the view can travel, counting this one.</param>
    private static void WalkSightBranch(
        SightNode node,
        SightTerrain parentTile,
        int remainingRange,
        IReadOnlyDictionary<(int X, int Y), SightTerrain> terrainByCoord,
        List<SightTerrain> seenTiles,
        HashSet<(int X, int Y)> seenCoords,
        HashSet<(int X, int Y)> visitedCoords)
    {
        if (remainingRange <= 0)
        {
            return;
        }

        // Guards against cycles and repeated traversal of the same tile within this Observer's walk.
        if (visitedCoords.Add((node.X, node.Y)) == false)
        {
            return;
        }

        // Terrain that was not loaded is ground the view cannot be judged across, so the branch ends here unseen.
        if (terrainByCoord.TryGetValue((node.X, node.Y), out SightTerrain? tile) == false)
        {
            return;
        }

        if (seenCoords.Add((tile.X, tile.Y)))
        {
            seenTiles.Add(tile);
        }

        // A blocking Biome, or ground higher than the tile the view arrived from, is itself seen - and hides everything behind it.
        bool biomeBlocksSight = tile.DoesBlockSight;
        bool elevationBlocksSight = tile.RelativeElevation > parentTile.RelativeElevation;

        if (biomeBlocksSight || elevationBlocksSight)
        {
            return;
        }

        int nextRemainingRange = remainingRange - 1;

        if (nextRemainingRange <= 0)
        {
            return;
        }

        foreach (SightNode child in node.Children)
        {
            WalkSightBranch(child, tile, nextRemainingRange, terrainByCoord, seenTiles, seenCoords, visitedCoords);
        }
    }

    /// <summary>
    /// Adds the adjacent tiles a blinded Observer's movement domain could never have entered to look at.
    /// </summary>
    /// <remarks>
    /// Every neighbour it could have walked onto stays dark, which is what keeps dense terrain worth hiding in. The judgement is deferred wholesale to
    /// <see cref="BlockedSightReveal"/>, the same authority a step asks.
    /// </remarks>
    private static void RevealGroundTheObserverCannotReach(
        SightObserver observer,
        CampaignMapGeometry geometry,
        IReadOnlyDictionary<(int X, int Y), SightTerrain> terrainByCoord,
        IEnumerable<IMovementDomainStrategy> movementDomainStrategies,
        List<SightTerrain> seenTiles,
        HashSet<(int X, int Y)> seenCoords)
    {
        // Kept as pairs so a revealed movement snapshot can be traced back to the terrain it was built from, in neighbour order.
        List<(SightTerrain Terrain, MovementTargetTile Snapshot)> candidates =
        [
            .. GetNeighbours(observer.Location, geometry)
                .Where(neighbour => terrainByCoord.ContainsKey((neighbour.X, neighbour.Y)))
                .Select(neighbour => terrainByCoord[(neighbour.X, neighbour.Y)])
                .Select(terrain => (terrain, terrain.ToMovementTargetTile()))
        ];

        IReadOnlyCollection<MovementTargetTile> revealed = BlockedSightReveal.SelectRevealedTiles(
            movementDomainStrategies,
            observer.MovementDomain,
            candidates.Select(candidate => candidate.Snapshot));

        foreach ((SightTerrain terrain, MovementTargetTile snapshot) in candidates)
        {
            if (revealed.Contains(snapshot) && seenCoords.Add((terrain.X, terrain.Y)))
            {
                seenTiles.Add(terrain);
            }
        }
    }

    private static IReadOnlyCollection<Coord> GetNeighbours(Coord origin, CampaignMapGeometry geometry)
    {
        return MapDirections.GetNeighbours(origin, geometry.Width, geometry.Height, geometry.WrapsHorizontally, geometry.WrapsVertically);
    }
}
