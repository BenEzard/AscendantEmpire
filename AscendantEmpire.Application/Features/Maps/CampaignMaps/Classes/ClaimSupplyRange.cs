using AscendantEmpire.BackendContracts.CampaignMaps;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

/// <summary>
/// Lapses the claims a Player cannot supply from their own owned land.
/// </summary>
/// <remarks>
/// <para>
/// The Claim supply range of <c>docs/design/campaign-map/tile-ownership.md</c>, and the whole of the anti-land-grab rule. It does three jobs with one number: a scout
/// cannot plant a claim on a distant chokepoint and keep it, a Player's total claims are bounded by roughly their owned perimeter times the hop count, and the depth of
/// an enclosed interior that can be held is capped. Because that one number does all three, there is deliberately no claim budget and no region size cap.
/// </para>
/// <para>
/// Pure, and run per Player: the walk only ever touches that Player's own territory, seeded from the working set's index rather than by scanning the map.
/// </para>
/// </remarks>
internal static class ClaimSupplyRange
{
    /// <summary>
    /// How many hops a claim may be from owned land before it lapses.
    /// </summary>
    /// <remarks>A first value, chosen to be tuned: if claim spam becomes a problem, this is the number to change.</remarks>
    internal const int MAX_SUPPLY_HOPS = 3;

    /// <summary>
    /// Lapses every claim of <paramref name="playerId"/> that cannot reach one of their own owned tiles within <see cref="MAX_SUPPLY_HOPS"/>.
    /// </summary>
    /// <param name="friendlyPlayerIds">
    /// The Players whose land this chain may run through. Just <paramref name="playerId"/> today. An ally's claimed and owned land counts towards the range, and this is
    /// the parameter that will carry it - so alliances become a change to the caller rather than to the walk.
    /// </param>
    internal static void Lapse(TerritoryWorkingSet working, int playerId, IReadOnlySet<int> friendlyPlayerIds)
    {
        ArgumentNullException.ThrowIfNull(working);
        ArgumentNullException.ThrowIfNull(friendlyPlayerIds);

        // Snapshotted because lapsing mutates the working set's index as we go.
        (int X, int Y)[] claimedTiles = [.. working.GetClaimedTiles(playerId)];

        if (claimedTiles.Length == 0)
        {
            return;
        }

        IReadOnlySet<(int X, int Y)> inRange = FindTilesInRange(working, friendlyPlayerIds);

        foreach ((int X, int Y) claimed in claimedTiles)
        {
            if (inRange.Contains(claimed) == false)
            {
                working.LapseClaim(claimed, playerId);
            }
        }
    }

    /// <summary>
    /// Every tile within <see cref="MAX_SUPPLY_HOPS"/> of friendly owned land, walking only through friendly ground that carries a hop.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A multi-source breadth-first walk, so "within 3 hops of <i>any</i> owned tile" is one traversal rather than one per claim. No iteration to a fixed point is
    /// needed: a distance is not a recurrence.
    /// </para>
    /// <para>
    /// <b>No river test anywhere, deliberately.</b> Rivers shape enclosure and impassability shapes supply; a chain crosses a river freely. The two mountain
    /// consequences then fall out with no code of their own: an owned mountain is not a source and is never walked through, so it is a dead end rather than a bridge;
    /// and a claimed mountain is never reached at all, which is why a mountain is only ever owned.
    /// </para>
    /// </remarks>
    private static IReadOnlySet<(int X, int Y)> FindTilesInRange(TerritoryWorkingSet working, IReadOnlySet<int> friendlyPlayerIds)
    {
        Dictionary<(int X, int Y), int> hopsByCoord = [];
        Queue<(int X, int Y)> frontier = new();

        foreach (int friendlyPlayerId in friendlyPlayerIds)
        {
            foreach ((int X, int Y) owned in working.GetOwnedTiles(friendlyPlayerId))
            {
                TerritoryTile? tile = working.Map.Get(owned);

                // Owned ground that cannot be walked cannot anchor a chain either.
                if (tile is null || TerritoryClaimability.CarriesSupplyHop(tile) == false)
                {
                    continue;
                }

                if (hopsByCoord.TryAdd(owned, 0))
                {
                    frontier.Enqueue(owned);
                }
            }
        }

        while (frontier.Count > 0)
        {
            (int X, int Y) current = frontier.Dequeue();
            int hops = hopsByCoord[current];

            if (hops == MAX_SUPPLY_HOPS)
            {
                continue;
            }

            TerritoryTile? currentTile = working.Map.Get(current);

            if (currentTile is null)
            {
                continue;
            }

            foreach (Coord neighbourCoord in working.Map.GetNeighbours(currentTile.Location))
            {
                (int X, int Y) key = (neighbourCoord.X, neighbourCoord.Y);

                if (hopsByCoord.ContainsKey(key))
                {
                    continue;
                }

                TerritoryTile? neighbour = working.Map.Get(key);

                if (neighbour is null || TerritoryClaimability.CarriesSupplyHop(neighbour) == false || IsFriendlyGround(working, key, friendlyPlayerIds) == false)
                {
                    continue;
                }

                hopsByCoord[key] = hops + 1;
                frontier.Enqueue(key);
            }
        }

        return hopsByCoord.Keys.ToHashSet();
    }

    /// <summary>
    /// Whether the chain may run through this tile - friendly owned or friendly claimed land, and nothing else.
    /// </summary>
    /// <remarks>A rival's ground never carries a chain, however they hold it, so a claim routed only through a neighbour's land lapses.</remarks>
    private static bool IsFriendlyGround(TerritoryWorkingSet working, (int X, int Y) coord, IReadOnlySet<int> friendlyPlayerIds)
    {
        if (working.GetOwner(coord) is int owner)
        {
            return friendlyPlayerIds.Contains(owner);
        }

        return working.GetClaimants(coord).Any(friendlyPlayerIds.Contains);
    }
}
