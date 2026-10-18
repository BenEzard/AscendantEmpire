using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

/// <summary>
/// Turns a tile's ownership columns, its claims and any battle on it into the state the map read reports.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read off the tile, never cached on it.</b> Claims are many-to-one, so there is no winner to store and no state column to read; this runs on every map read instead. See
/// "The states" and "How it is stored" in <c>docs/design/campaign-map/tile-ownership.md</c>.
/// </para>
/// <para>
/// Pure, and deliberately separate from the query that loads the columns, in the same spirit as <see cref="TileRiverDerivation"/>: the precedence between the layers is the
/// interesting part and it is testable with no database at all.
/// </para>
/// <para>
/// <b>The precedence is the exclusive layers first, then claims.</b> Owned, Occupied, Disputed and Abandoned are each a single Player's and cannot coexist on a tile, so one
/// of them always answers outright when it applies. Claimed is the only non-exclusive state, and ownership extinguishes claims rather than sharing a tile with them, so it is
/// reached only once every exclusive layer has come back empty.
/// </para>
/// <para>
/// <b>There is no viewer, yet.</b> The design has the displayed state resolved per Player, because a hidden Force is visible to some Players and not others - so two of them
/// can correctly see one tile differently. Nothing hides anything today: ambush and recon stances wait on hiding and detection
/// (<c>docs/design/campaign-map/vision.md</c>), and the claimants are named to anyone who can see the ground, since a claim is asserted by a marker standing on it. So what
/// this resolves is the same for everybody, and the viewer comes back as a parameter when there is something for it to change.
/// </para>
/// </remarks>
internal static class TileOwnershipDerivation
{
    /// <summary>
    /// The state of one tile, from its columns and whatever is on it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="claimantPlayerIds"/> is read rather than <see cref="TileOwnershipColumns.HasClaims"/>: the flag is the reader's licence to skip the junction, and a
    /// tile whose flag said <see langword="true"/> but whose rows have since gone resolves as unclaimed rather than as claimed by nobody.
    /// </para>
    /// <para>
    /// <paramref name="hasBattle"/> is handed in rather than derived here, because Disputed is the one state with nothing on the tile behind it - it is a
    /// <c>BattleEngagement</c> on the tile and is never stored.
    /// </para>
    /// </remarks>
    /// <param name="columns">The tile's own ownership columns.</param>
    /// <param name="claimantPlayerIds">Every Player claiming this tile. Empty for the overwhelming majority of tiles, which have no claim rows at all.</param>
    /// <param name="hasBattle">Whether a battle is being fought on this tile right now.</param>
    internal static TileOwnership Derive(TileOwnershipColumns columns, IReadOnlyCollection<int> claimantPlayerIds, bool hasBattle)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(claimantPlayerIds);

        TileOwnershipStatus status = ResolveStatus(columns, claimantPlayerIds.Count > 0, hasBattle);

        // The one case worth not allocating for: wilderness, which most of a map is, and which carries nothing but the status.
        if (status == TileOwnershipStatus.Unclaimed)
        {
            return TileOwnership.Unclaimed;
        }

        return new TileOwnership
        {
            Status = status,
            OwnedByPlayerId = columns.OwnedByPlayerId,
            OccupiedByPlayerId = columns.OccupiedByPlayerId,
            LastOwnedByPlayerId = columns.LastOwnedByPlayerId,
            // Sorted here rather than relied on from the query, because the client draws the claimants in this order - the border alternates their colours along its dashes,
            // and the tooltip lists them to match. An order that shifted between reads would make the dashes swap colours for no reason.
            ClaimedByPlayerIds = [.. claimantPlayerIds.Order()],
            // Rebellion is a modifier on Owned rather than a state, so it rides alongside whatever the status came out as. The percentage itself is not reported: how far
            // through its rebellion a tile is would tell the conqueror's rival exactly when it settles.
            IsRebelling = columns.RebellionPercentRemaining is not null
        };
    }

    /// <summary>
    /// Which of the six states the tile is in.
    /// </summary>
    /// <remarks>
    /// The precedence between the layers, and nothing else. Each branch is the design's own wording:
    /// <list type="bullet">
    /// <item><b>Disputed</b> first, because both sides' Forces being on the tile outranks whoever holds the ground under them, and nobody collects the yield while they fight.</item>
    /// <item><b>Occupied</b> next: a rival's Forces holding the tile alone, with the owner keeping ownership throughout - so this wins over Owned rather than replacing it.</item>
    /// <item><b>Owned</b>, the exclusive administrative tier everything else hangs off.</item>
    /// <item><b>Abandoned</b>, which only arises where there is no owner: it is what a razed Settlement's tiles become, and it remembers who held them.</item>
    /// <item><b>Claimed</b> last of the held states, since ownership extinguishes claims and the two never coexist.</item>
    /// </list>
    /// </remarks>
    private static TileOwnershipStatus ResolveStatus(TileOwnershipColumns columns, bool isClaimed, bool hasBattle)
    {
        if (hasBattle)
        {
            return TileOwnershipStatus.Disputed;
        }

        if (columns.OccupiedByPlayerId is not null)
        {
            return TileOwnershipStatus.Occupied;
        }

        if (columns.OwnedByPlayerId is not null)
        {
            return TileOwnershipStatus.Owned;
        }

        if (columns.LastOwnedByPlayerId is not null)
        {
            return TileOwnershipStatus.Abandoned;
        }

        if (isClaimed)
        {
            return TileOwnershipStatus.Claimed;
        }

        return TileOwnershipStatus.Unclaimed;
    }
}
