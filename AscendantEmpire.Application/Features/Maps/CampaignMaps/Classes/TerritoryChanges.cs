namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

/// <summary>
/// One tile's ownership columns as the territory pass decided they should be.
/// </summary>
/// <param name="CampaignMapId">The world-map tile the write lands on.</param>
/// <param name="OwnedByPlayerId">The new owner.</param>
/// <param name="OwnedBySettlementId">The Settlement the tile is owned <i>through</i>, or <see langword="null"/> for owned land that carries none.</param>
/// <param name="ClearsAbandonment">
/// Whether the write also clears the Abandoned stamps. An ownership grant does: "a transfer extinguishes the former claim or ownership", so a tile that was remembering
/// a former owner stops remembering it the moment somebody takes the ground.
/// </param>
internal sealed record TileOwnershipWrite(int CampaignMapId, int? OwnedByPlayerId, int? OwnedBySettlementId, bool ClearsAbandonment);

/// <summary>
/// Everything the territory pass decided to change, as a diff against the state it was given.
/// </summary>
/// <remarks>
/// <para>
/// <b>A diff, not a list of events.</b> The sub-rules write into a <see cref="TerritoryWorkingSet"/> and this is the difference between that and the map it was built
/// from, computed once at the end. That is what makes a claim proposed by enclosure and then lapsed by the supply range in the same pass produce nothing at all, rather
/// than a row inserted and deleted every Round for ever.
/// </para>
/// <para>
/// The two tile collections are keyed by tile id and the two claim collections by <c>(tile, Player)</c>, so one tile cannot appear twice. That is load-bearing rather
/// than tidy: <c>BuildingStructure</c> accumulates the writes of every Structure that completed in a Turn into one of these, and two Settlements with overlapping
/// territory would otherwise stage two claim stubs with the same composite key - which EF refuses with a change-tracking exception.
/// </para>
/// </remarks>
internal sealed record TerritoryChanges
{
    /// <summary>Tiles whose ownership columns changed, keyed by world-map tile id.</summary>
    public required IReadOnlyDictionary<int, TileOwnershipWrite> OwnershipWrites { get; init; }

    /// <summary>Claim rows to insert.</summary>
    public required IReadOnlySet<(int CampaignMapId, int PlayerId)> ClaimsToAdd { get; init; }

    /// <summary>Claim rows to delete - lapsed out of supply range, or extinguished by somebody taking ownership of the tile.</summary>
    public required IReadOnlySet<(int CampaignMapId, int PlayerId)> ClaimsToRemove { get; init; }

    /// <summary>
    /// Tiles whose <c>HasClaims</c> flag must change, keyed by tile id.
    /// </summary>
    /// <remarks>
    /// Moves in the same save as the rows above, so the flag and the junction are never observably out of step. A tile can appear here on its own, with no claim change
    /// at all, when the flag was simply wrong: the pass holds the complete claim set for the Game, so it repairs a stale flag as a side effect of knowing better.
    /// </remarks>
    public required IReadOnlyDictionary<int, bool> HasClaimsWrites { get; init; }

    /// <summary>Whether there is nothing to write, which is the ordinary case for a Round in which no border moved.</summary>
    public bool IsEmpty => OwnershipWrites.Count == 0
                        && ClaimsToAdd.Count == 0
                        && ClaimsToRemove.Count == 0
                        && HasClaimsWrites.Count == 0;

    /// <summary>How many tiles the writer will have to load, for the log line.</summary>
    public int AffectedTileCount => OwnershipWrites.Keys.Union(HasClaimsWrites.Keys).Count();

    /// <summary>A diff that changes nothing.</summary>
    internal static TerritoryChanges Empty { get; } = new()
    {
        OwnershipWrites = new Dictionary<int, TileOwnershipWrite>(),
        ClaimsToAdd = new HashSet<(int, int)>(),
        ClaimsToRemove = new HashSet<(int, int)>(),
        HasClaimsWrites = new Dictionary<int, bool>()
    };
}
