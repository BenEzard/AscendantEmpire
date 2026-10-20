using AscendantEmpire.BackendContracts.CampaignMaps;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

/// <summary>
/// Absorbs wilderness that one Player's territory completely rings.
/// </summary>
/// <remarks>
/// <para>
/// The Enclosure rules of <c>docs/design/campaign-map/tile-ownership.md</c>. Pure: no database, no side effects beyond the working set it is handed.
/// </para>
/// <para>
/// <b>Two kinds of seal, and they work differently.</b> A <i>river edge</i> seals by removing the adjacency, so the tile across it is neither a region member nor a
/// boundary tile - which is what lets a rival on the far bank of a river be irrelevant. <i>Impassable terrain</i> and the Void seal as <i>boundary tiles</i>: they are
/// excluded from the region and count as neutral rather than as a veto.
/// </para>
/// </remarks>
internal static class TerritoryEnclosure
{
    /// <summary>
    /// What a boundary tile does to the region it borders.
    /// </summary>
    private enum BoundaryEffect
    {
        /// <summary>One Player's ground, which counts towards the ring and contributes its tier.</summary>
        RingMember,

        /// <summary>Neutral ground nothing can cross, which closes the ring without belonging to anybody.</summary>
        Seal,

        /// <summary>Somebody else's interest, which refuses the absorption outright.</summary>
        Veto,

        /// <summary>Wilderness, which means the region is not closed here at all.</summary>
        Open
    }

    /// <summary>
    /// Absorbs every region of wilderness that a single Player rings completely, writing the grants into <paramref name="working"/>.
    /// </summary>
    internal static void Absorb(TerritoryWorkingSet working)
    {
        ArgumentNullException.ThrowIfNull(working);

        AbsorbPassableRegions(working);
        AbsorbImpassableTiles(working);
    }

    /// <summary>
    /// Sweeps the map for connected regions of passable wilderness and absorbs the ones a single Player rings.
    /// </summary>
    /// <remarks>
    /// <b>One sweep is provably enough.</b> Regions are the maximal connected components of absorbable passable tiles, so absorbing one can never close another: a
    /// component vetoed by a rival is not un-vetoed by a grant elsewhere, and one component's boundary can never hold another's tiles, because an unheld passable
    /// boundary tile would have been <i>inside</i> the region to begin with.
    /// </remarks>
    private static void AbsorbPassableRegions(TerritoryWorkingSet working)
    {
        HashSet<(int X, int Y)> visited = [];

        // Ordered so the sweep is deterministic whatever order the reader loaded the tiles in.
        foreach (TerritoryTile tile in working.Map.TilesByCoord.Values.OrderBy(t => t.CampaignMapId))
        {
            if (visited.Contains(tile.Key) || IsAbsorbable(working, tile) == false || tile.IsImpassable)
            {
                continue;
            }

            GrowAndAbsorbRegion(working, tile, visited);
        }
    }

    /// <summary>
    /// Flood-fills the region containing <paramref name="seed"/>, works out what rings it, and absorbs it if one Player rings it completely.
    /// </summary>
    private static void GrowAndAbsorbRegion(TerritoryWorkingSet working, TerritoryTile seed, HashSet<(int X, int Y)> visited)
    {
        List<(int X, int Y)> members = [];
        Queue<TerritoryTile> frontier = new();

        // The columns and rows the region occupies. A region covering every one of them on an axis that wraps has met itself the long way round, which is the torus's
        // version of running off the edge of the map - see TerritoryMap.IsBounded.
        HashSet<int> columns = [];
        HashSet<int> rows = [];

        frontier.Enqueue(seed);
        visited.Add(seed.Key);

        // The Players whose ground forms the ring. More than one means no single Player rings it, so the absorption is refused the same way a rival veto refuses it.
        HashSet<int> ringPlayerIds = [];
        bool ringIsOwnedThroughout = true;
        bool isClosed = true;

        while (frontier.Count > 0)
        {
            TerritoryTile current = frontier.Dequeue();
            members.Add(current.Key);
            columns.Add(current.X);
            rows.Add(current.Y);

            // A sealing river edge is not an adjacency at all, so the tile beyond it is never reached - neither as a member nor as a boundary.
            foreach (Coord neighbourCoord in working.Map.GetUnsealedNeighbours(current.Location))
            {
                (int X, int Y) key = (neighbourCoord.X, neighbourCoord.Y);
                TerritoryTile? neighbour = working.Map.Get(key);

                if (neighbour is null)
                {
                    continue;
                }

                // Absorbable passable ground is part of the same region, so it is walked rather than judged.
                if (IsAbsorbable(working, neighbour) && neighbour.IsImpassable == false)
                {
                    if (visited.Add(key))
                    {
                        frontier.Enqueue(neighbour);
                    }

                    continue;
                }

                ApplyBoundary(working, neighbour, ringPlayerIds, ref ringIsOwnedThroughout, ref isClosed);
            }
        }

        // A region that is not bounded was never surrounded by anybody, whatever its boundary tiles say.
        AbsorbIfRinged(working, members, ringPlayerIds, ringIsOwnedThroughout, isClosed && working.Map.IsBounded(columns, rows));
    }

    /// <summary>
    /// Absorbs impassable wilderness, one tile at a time, repeating until nothing more is taken.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An impassable tile seals, so it is never a member of a passable region - which means a mountain pocket entirely inside a border has no region to be absorbed
    /// with, and needs its own sweep. Each such tile is a region of exactly itself, because the fill never expands out of impassable ground.
    /// </para>
    /// <para>
    /// Iterated because a chain of mountains only falls one link at a time: the outer one is not ringed until the inner one has become owned. Monotone - tiles only ever
    /// gain a holder - so it terminates, and the candidate list is tiny, being the unheld impassable tiles alone.
    /// </para>
    /// </remarks>
    private static void AbsorbImpassableTiles(TerritoryWorkingSet working)
    {
        List<TerritoryTile> candidates = [.. working.Map.TilesByCoord.Values
            .Where(tile => tile.IsImpassable && tile.IsVoid == false)
            .OrderBy(tile => tile.CampaignMapId)];

        bool absorbedAnything = true;

        while (absorbedAnything && candidates.Count > 0)
        {
            absorbedAnything = false;
            List<TerritoryTile> remaining = [];

            foreach (TerritoryTile tile in candidates)
            {
                if (IsAbsorbable(working, tile) == false)
                {
                    continue;
                }

                HashSet<int> ringPlayerIds = [];
                bool ringIsOwnedThroughout = true;

                // A single tile is always bounded, so only its boundary decides this one.
                bool isClosed = working.Map.IsBounded([tile.X], [tile.Y]);

                foreach (Coord neighbourCoord in working.Map.GetUnsealedNeighbours(tile.Location))
                {
                    TerritoryTile? neighbour = working.Map.Get((neighbourCoord.X, neighbourCoord.Y));

                    if (neighbour is null)
                    {
                        continue;
                    }

                    ApplyBoundary(working, neighbour, ringPlayerIds, ref ringIsOwnedThroughout, ref isClosed);
                }

                if (AbsorbIfRinged(working, [tile.Key], ringPlayerIds, ringIsOwnedThroughout, isClosed))
                {
                    absorbedAnything = true;

                    continue;
                }

                remaining.Add(tile);
            }

            candidates = remaining;
        }
    }

    /// <summary>
    /// Folds one boundary tile into the running judgement of the ring.
    /// </summary>
    private static void ApplyBoundary(
        TerritoryWorkingSet working,
        TerritoryTile boundary,
        HashSet<int> ringPlayerIds,
        ref bool ringIsOwnedThroughout,
        ref bool isClosed)
    {
        switch (Classify(working, boundary, out int ringPlayerId, out bool isOwned))
        {
            case BoundaryEffect.RingMember:
                ringPlayerIds.Add(ringPlayerId);

                if (isOwned == false)
                {
                    // The interior takes the weakest tier in the ring, so one merely claimed tile anywhere makes the whole grant a claim.
                    ringIsOwnedThroughout = false;
                }

                break;

            case BoundaryEffect.Veto:
            case BoundaryEffect.Open:
                isClosed = false;

                break;

            case BoundaryEffect.Seal:
                break;
        }
    }

    /// <summary>
    /// What one boundary tile does to the region beside it.
    /// </summary>
    /// <param name="ringPlayerId">The Player whose ground it is, when it is a ring member.</param>
    /// <param name="isOwned">Whether that Player owns it rather than merely claiming it.</param>
    private static BoundaryEffect Classify(TerritoryWorkingSet working, TerritoryTile boundary, out int ringPlayerId, out bool isOwned)
    {
        ringPlayerId = 0;
        isOwned = false;

        // Nothing can cross the Void, and nobody can own it, so it closes a ring without belonging to anyone.
        if (boundary.IsVoid)
        {
            return BoundaryEffect.Seal;
        }

        // A rival standing on the ground is an interest in it however the columns underneath read.
        if (boundary.OccupiedByPlayerId is not null)
        {
            return BoundaryEffect.Veto;
        }

        // Abandoned ground is held by nobody but is not wilderness: it still remembers a former owner, and absorbing it would let a neighbour inherit a razed empire.
        if (working.GetOwner(boundary.Key) is null && working.GetClaimants(boundary.Key).Count == 0)
        {
            if (boundary.LastOwnedByPlayerId is not null)
            {
                return BoundaryEffect.Veto;
            }

            // Unheld impassable terrain is the second seal: neutral, uncrossable, and not a member because the far side of a mountain wall is none of the region's business.
            return boundary.IsImpassable ? BoundaryEffect.Seal : BoundaryEffect.Open;
        }

        if (working.GetOwner(boundary.Key) is int owner)
        {
            ringPlayerId = owner;
            isOwned = true;

            return BoundaryEffect.RingMember;
        }

        IReadOnlyCollection<int> claimants = working.GetClaimants(boundary.Key);

        // Several Players claiming one boundary tile is not one Player's ring, so it refuses rather than contributing an arbitrary one of them.
        if (claimants.Count != 1)
        {
            return BoundaryEffect.Veto;
        }

        ringPlayerId = claimants.Single();
        isOwned = false;

        return BoundaryEffect.RingMember;
    }

    /// <summary>
    /// Grants the region to the Player who rings it, if exactly one does and nothing refused.
    /// </summary>
    /// <remarks>
    /// The grant is applied <b>per tile</b>, because the ring's tier and what a given tile can take are different questions: an impassable tile cannot be claimed, so a
    /// claimed ring leaves a mountain in its interior unclaimed rather than writing a claim that would lapse the same Round.
    /// </remarks>
    /// <returns>Whether anything was absorbed.</returns>
    private static bool AbsorbIfRinged(
        TerritoryWorkingSet working,
        IReadOnlyCollection<(int X, int Y)> members,
        HashSet<int> ringPlayerIds,
        bool ringIsOwnedThroughout,
        bool isClosed)
    {
        if (isClosed == false || ringPlayerIds.Count != 1)
        {
            return false;
        }

        int playerId = ringPlayerIds.Single();
        bool absorbedAnything = false;

        foreach ((int X, int Y) member in members)
        {
            TerritoryTile? tile = working.Map.Get(member);

            if (tile is null)
            {
                continue;
            }

            if (ringIsOwnedThroughout)
            {
                if (TerritoryClaimability.CanOwn(tile, working.GetOwner(member), playerId) == false)
                {
                    continue;
                }

                // Enclosure grants bare ownership: no Settlement reaches it, so no Settlement-scoped modifier applies to it.
                working.AssertOwnership(member, playerId, settlementId: null);
                absorbedAnything = true;

                continue;
            }

            if (TerritoryClaimability.CanClaim(tile, working.GetOwner(member), playerId) == false)
            {
                continue;
            }

            working.AssertClaim(member, playerId);
            absorbedAnything = true;
        }

        return absorbedAnything;
    }

    /// <summary>
    /// Whether this tile is wilderness the pass may still absorb, read off the working set so a grant made earlier in the pass counts.
    /// </summary>
    private static bool IsAbsorbable(TerritoryWorkingSet working, TerritoryTile tile)
    {
        return tile.IsVoid == false && working.IsHeld(tile.Key) == false;
    }
}
