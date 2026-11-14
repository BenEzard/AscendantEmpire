import type { ObservedStructureTile, TileCell } from "@/features/map/components/TileTypes";

/**
 * The player's own settlement standing on a tile, or null where there is none to manage.
 *
 * <remarks>
 * <para>
 * A settlement is not its own thing on the map read: it arrives as a structure carrying a name, and a structure with no name is
 * nobody's settlement — a watchtower, a totem pole. A tile holds at most one, so the first match is the answer rather than a
 * choice between several.
 * </para>
 * <para>
 * Whose it is comes from the tile rather than the structure, which carries no owner. An occupier takes no ownership, so a
 * settlement under enemy occupation is still the owner's to manage — hence `ownedByPlayerId` and not `effectiveHolderPlayerId`.
 * </para>
 * <para>
 * Ground the player only remembers reports no ownership at all, and that absence is left to mean nothing is owned: "we have not
 * been told" must not resolve to "it is yours".
 * </para>
 * </remarks>
 */
export function ownedSettlementOnTile(tile: TileCell, playerId: number | null | undefined): ObservedStructureTile | null {
  if (playerId == null || tile.ownership?.ownedByPlayerId !== playerId) {
    return null;
  }

  return tile.structures?.find((structure) => structure.settlementName != null) ?? null;
}
