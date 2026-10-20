namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

/// <summary>
/// What a Player may take, and what carries their reach, over one campaign map tile.
/// </summary>
/// <remarks>
/// <para>
/// Three predicates shared by everything that grants territory - a Settlement's radius, a Structure's assertion and enclosure absorption - so the three cannot end up
/// disagreeing about what a mountain is. The rules are in <c>docs/design/campaign-map/tile-ownership.md</c>; this is their single implementation.
/// </para>
/// <para>
/// <b>Water is absent from all three.</b> It is ordinary terrain for ownership, so there is nothing to say about it: a coastal Settlement's radius takes the sea beside
/// it exactly as it takes the land, and open ocean belongs to nobody because nobody settles out there - not because a rule forbids it.
/// </para>
/// </remarks>
internal static class TerritoryClaimability
{
    /// <summary>
    /// Whether <paramref name="playerId"/> may take this tile as owned.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Void never, because nothing can ever own it. A rival's owned or occupied ground never, because ownership is exclusive and earned - building a village beside
    /// someone is not a conquest of them.
    /// </para>
    /// <para>
    /// Everything else yes, including two cases worth naming. <b>Impassable terrain</b> is ownable: a mountain inside a border is plausibly yours, and it simply carries
    /// no supply hop (see <see cref="CarriesSupplyHop"/>). A tile <b>claimed by anybody</b>, this Player or a rival, is ownable too, because ownership extinguishes every
    /// claim on the tile - that is the ordinary way a border pushes outwards.
    /// </para>
    /// </remarks>
    /// <param name="currentOwnerPlayerId">Who owns the tile as the pass has it <i>now</i>, which may already differ from <paramref name="tile"/>.</param>
    internal static bool CanOwn(TerritoryTile tile, int? currentOwnerPlayerId, int playerId)
    {
        ArgumentNullException.ThrowIfNull(tile);

        if (tile.IsVoid)
        {
            return false;
        }

        // A rival standing on the ground holds it until they are removed, whoever owns it underneath.
        if (tile.OccupiedByPlayerId is int occupier && occupier != playerId)
        {
            return false;
        }

        // Already this Player's is not a refusal, just nothing to do; the caller's diff drops it.
        return currentOwnerPlayerId is null || currentOwnerPlayerId == playerId;
    }

    /// <summary>
    /// Whether <paramref name="playerId"/> may take this tile as claimed.
    /// </summary>
    /// <remarks>
    /// Stricter than <see cref="CanOwn"/> in two ways. Ground <b>owned by anybody</b>, this Player included, cannot be claimed, because a claim and ownership never
    /// coexist on one tile. And <b>impassable terrain</b> cannot be claimed either: a claimed mountain carries no hop, so it could never reach owned land and would lapse
    /// the Round it arrived - which is why a mountain is only ever owned, and why that needs no rule of its own.
    /// </remarks>
    /// <param name="currentOwnerPlayerId">Who owns the tile as the pass has it <i>now</i>, which may already differ from <paramref name="tile"/>.</param>
    internal static bool CanClaim(TerritoryTile tile, int? currentOwnerPlayerId, int playerId)
    {
        ArgumentNullException.ThrowIfNull(tile);

        if (tile.IsVoid || tile.IsImpassable)
        {
            return false;
        }

        if (tile.OccupiedByPlayerId is int occupier && occupier != playerId)
        {
            return false;
        }

        return currentOwnerPlayerId is null;
    }

    /// <summary>
    /// Whether this tile carries a hop for the claim supply range.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Passability, and nothing else. Rivers shape enclosure and impassability shapes supply, and neither rule carries the other's clause - a chain crosses a river
    /// freely, and stops dead at a mountain.
    /// </para>
    /// <para>
    /// That single fact spends both halves of the mountain rule: an owned mountain is a dead end rather than a bridge, and a claimed one could never be reached. Water
    /// is passable, so a chain runs across a strait with no water clause anywhere.
    /// </para>
    /// </remarks>
    internal static bool CarriesSupplyHop(TerritoryTile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);

        return tile.IsImpassable == false;
    }
}
