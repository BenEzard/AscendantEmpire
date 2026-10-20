namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

/// <summary>
/// The per-Round territory pass: works out how a Game's borders should stand, and hands back the difference.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="FieldOfView"/> of ownership, and the class <c>docs/design/campaign-map/tile-ownership.md</c> asks for: "a pure class run over an in-memory bitmap with
/// deltas written back, the way <c>FieldOfView</c> is shared between the map read and the witness service, not in SQL". No database, no side effects: the caller loads
/// the tiles and writes the answer.
/// </para>
/// <para>
/// <b>Monotone, and therefore idempotent.</b> Every rule here only ever <i>grants</i> ownership or takes a claim away, and both are decided from the tile columns and
/// the claim rows rather than from anything a previous run left behind. Running it twice on the same state gives an empty diff the second time, which is what lets it
/// sit in a turn pipeline that is not transactional and carries no per-Round stamp - unlike every accumulating process beside it.
/// </para>
/// </remarks>
internal static class TileTerritory
{
    /// <summary>
    /// Resolves one Game's territory and returns what should change.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Enclosure runs before claim lapse</b>, and that order is load-bearing. The hop count is supposed to cap how deep an enclosed interior can be held, which is
    /// only true if the claims enclosure grants are judged by the supply range in the same pass. The other way round, the middle of a wide pocket would be written as
    /// claimed, survive a Round, lapse, be re-absorbed and lapse again - inserting and deleting the same row for ever.
    /// </para>
    /// <para>
    /// There is no outer loop over the two rules. Enclosure's own impassable sweep iterates to a fixed point, because a chain of mountains falls one link at a time, and
    /// the supply range is a distance rather than a recurrence and so needs one walk.
    /// </para>
    /// </remarks>
    /// <param name="map">Every tile of the Game, as loaded.</param>
    /// <param name="playerIdsInTurnOrder">
    /// The Players to resolve, earliest in the turn queue first. Order matters wherever two of them could want one tile: the earlier Player gets it, rather than the
    /// answer depending on how a dictionary enumerated.
    /// </param>
    internal static TerritoryChanges Resolve(TerritoryMap map, IReadOnlyCollection<int> playerIdsInTurnOrder)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(playerIdsInTurnOrder);

        TerritoryWorkingSet working = TerritoryWorkingSet.From(map, playerIdsInTurnOrder);

        TerritoryEnclosure.Absorb(working);

        foreach (int playerId in working.PlayerIds)
        {
            // One id today. An ally's claimed and owned land counts towards the range, and this is where the set grows once alliances are modelled.
            ClaimSupplyRange.Lapse(working, playerId, new HashSet<int> { playerId });
        }

        return working.ToChanges();
    }
}
