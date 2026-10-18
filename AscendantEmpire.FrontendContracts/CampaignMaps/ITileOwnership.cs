using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.FrontendContracts.CampaignMaps;

/// <summary>
/// Who holds one campaign map tile, as one Player sees it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read off the tile, never cached on it.</b> Claims are many-to-one, so there is no winner to store and no single state column to send; the server reads the states off
/// the tile's nullable Player references and the claim junction on every map read - see "How it is stored" in <c>docs/design/campaign-map/tile-ownership.md</c>.
/// </para>
/// <para>
/// <b>Reported only while an Observer can see the tile.</b> Ownership is current information rather than terrain: unlike the Biome and the rivers, which never change once
/// explored, a border moves. Ground the Player merely remembers comes back with this absent, the same way its Forces and Structures do, so the client draws a border only
/// where someone is actually watching.
/// </para>
/// </remarks>
public interface ITileOwnership
{
    /// <summary>
    /// The tile's state for this viewer.
    /// </summary>
    /// <remarks>
    /// The exclusive layers win over each other in the order the design gives them: a battle makes the tile <c>Disputed</c> whoever owns it, an occupier outranks the owner
    /// it is standing on, and ownership extinguishes claims rather than coexisting with them. <c>Claimed</c> therefore only ever describes an unowned tile, and says nothing
    /// on its own about <i>whose</i> claim it is - that is what the two flags below are for.
    /// </remarks>
    TileOwnershipStatus Status { get; }

    /// <summary>
    /// The Player who owns the tile administratively, or <see langword="null"/> when nobody does.
    /// </summary>
    /// <remarks>
    /// Sent for a rival's ground as readily as the viewer's own: a border the Player can see is a border they are meant to be able to read, and the colour it is drawn in
    /// comes from this.
    /// </remarks>
    int? OwnedByPlayerId { get; }

    /// <summary>
    /// The Player whose Forces are occupying the tile, or <see langword="null"/> when none are.
    /// </summary>
    /// <remarks>Set alongside <see cref="OwnedByPlayerId"/> rather than instead of it, because an occupier does not take ownership - the owner keeps the tile throughout.</remarks>
    int? OccupiedByPlayerId { get; }

    /// <summary>
    /// The Player who owned the tile before it was abandoned, or <see langword="null"/> when it is not abandoned.
    /// </summary>
    /// <remarks>Forgotten when the tile reverts to Unclaimed, so this is only ever set while <see cref="Status"/> reads <c>Abandoned</c>.</remarks>
    int? LastOwnedByPlayerId { get; }

    /// <summary>
    /// Who holds the tile right now - the occupier if there is one, otherwise the owner - or <see langword="null"/> when nobody does.
    /// </summary>
    /// <remarks>The answer to "who is drawing from this tile", carried so the client never has to re-derive the fallback and get it the other way round.</remarks>
    int? EffectiveHolderPlayerId { get; }

    /// <summary>
    /// Every Player claiming this tile, in Player id order. Empty on all but a handful of tiles.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The claimants are named, not counted.</b> A claim is asserted by something standing on the ground - a totem pole, a border marker, a scout's action - so a Player
    /// who can see the tile can see whose marker is on it, and withholding the name would hide what they could plainly observe. The client draws the claim border in each
    /// claimant's own faction colour and names them all in the tile's tooltip.
    /// </para>
    /// <para>
    /// <b>Ordered, because the order is drawn.</b> Several Players may claim one tile at once - claims are the only non-exclusive state - and the border alternates their
    /// colours along its dashes. A stable order keeps those dashes from swapping colours between reads, and keeps the tooltip's list in step with them.
    /// </para>
    /// <para>
    /// Sparse rather than cheap to ignore: the overwhelming majority of tiles have no claim rows at all, and <c>CampaignMap.HasClaims</c> is what lets the map read skip the
    /// junction for them. So this costs a few ints on the handful of tiles that are actually claimed, and nothing anywhere else.
    /// </para>
    /// </remarks>
    IReadOnlyCollection<int> ClaimedByPlayerIds { get; }

    /// <summary>
    /// Whether the tile is still rebelling against the Player who conquered it.
    /// </summary>
    /// <remarks>
    /// A modifier on <c>Owned</c>, never a state of its own, so the status beside it still reads <c>Owned</c>. The percentage behind it is deliberately not sent: how far
    /// through its rebellion a tile is tells the conqueror's rival exactly when it will settle.
    /// </remarks>
    bool IsRebelling { get; }
}
