/**
 * What state a campaign map tile is in — the `TileOwnershipStatus` enum's member names, since the API serialises enums as strings.
 *
 * The exclusive layers beat each other in a fixed order on the server, and `Claimed` only ever describes an *unowned* tile:
 * ownership extinguishes claims rather than coexisting with them. `Unknown` is the enum's zero member and should never arrive;
 * it is listed so a tile the API said nothing about still types.
 *
 * See docs/design/campaign-map/tile-ownership.md.
 */
export type TileOwnershipStatus = "Unknown" | "Unclaimed" | "Claimed" | "Owned" | "Occupied" | "Disputed" | "Abandoned";

/**
 * Who holds one tile.
 *
 * Read off the tile's columns and the claim junction on every map read, never cached on the tile: claims are many-to-one, so
 * there is no winner to store.
 *
 * Absent (rather than `Unclaimed`) on ground the player only remembers. Ownership is current information, unlike the biome and
 * the rivers, so the map read reports it only where an observer is actually watching: "nobody holds this" and "we have not been
 * told" are different statements, and only the first of them is worth drawing.
 */
export type TileOwnership = {
  status: TileOwnershipStatus;

  /** The owning player, or null when nobody owns the tile. Sent for rivals' ground too — a border you can see is one you can read. */
  ownedByPlayerId: number | null;

  /** The occupying player, or null. Set *alongside* the owner rather than instead of them: an occupier takes no ownership. */
  occupiedByPlayerId: number | null;

  /** Who owned the tile before it was abandoned, or null. Only ever set while the status reads `"Abandoned"`. */
  lastOwnedByPlayerId: number | null;

  /** The occupier if there is one, otherwise the owner — "who holds this". Resolved server-side so the fallback cannot be taken the wrong way round. */
  effectiveHolderPlayerId: number | null;

  /**
   * Every player claiming this tile, in player id order, and empty on all but a handful of tiles.
   *
   * Claims are the only non-exclusive state, so several players may claim one tile at once. The order is the server's and is
   * stable, because it is *drawn*: the claim border alternates the claimants' colours along its dashes, and the tooltip lists
   * them to match.
   */
  claimedByPlayerIds: number[];

  /** Whether the tile is still rebelling against whoever conquered it. A modifier on `"Owned"`, so the status beside it still reads `"Owned"`. */
  isRebelling: boolean;
};

/** The four sides of a tile, in the order borders are emitted. Matches RIVER_EDGES so the two overlays agree on what "north" means. */
export const TILE_EDGES = ["north", "east", "south", "west"] as const;

export type TileEdge = (typeof TILE_EDGES)[number];

/**
 * How a tile's state is named for the player, for the hover tooltip.
 *
 * `Unclaimed` has no label: most of the map is wilderness, and a line reading "Unclaimed" on every tile says nothing. `Unknown`
 * has none either — it only arrives from an API that told us nothing, and guessing at it would be worse than silence.
 */
const TILE_OWNERSHIP_LABELS: Record<TileOwnershipStatus, string | null> = {
  Unknown: null,
  Unclaimed: null,
  Claimed: "Claimed",
  Owned: "Owned",
  Occupied: "Occupied",
  Disputed: "Disputed",
  Abandoned: "Abandoned",
};

/** A faction's name, or a readable stand-in when the lookup has nothing for that id. */
function factionName(playerId: number, factionNameByPlayerId: ReadonlyMap<number, string>): string {
  return factionNameByPlayerId.get(playerId) ?? `Player #${playerId}`;
}

/**
 * The tile's state as one short line for a tooltip, or null when there is nothing to say.
 *
 * Reads `"Owned by Rome (rebelling)"`, or `"Claimed by Rome, Carthage"` where several players assert the same tile — claims are
 * not exclusive, so the line names every one of them, in the same order their colours appear in the border.
 *
 * Takes a faction-name lookup rather than resolved names, so the caller does not have to work out the holder itself and get the
 * occupier/owner fallback the wrong way round.
 */
export function describeTileOwnership(
  ownership: TileOwnership | undefined,
  factionNameByPlayerId: ReadonlyMap<number, string>,
): string | null {
  if (ownership == null) {
    return null;
  }

  const label = TILE_OWNERSHIP_LABELS[ownership.status];

  if (label == null) {
    return null;
  }

  // Claimed ground is named from the claimant list rather than from a holder: nobody *holds* a claimed tile, which is the whole
  // difference between a claim and ownership.
  if (ownership.status === "Claimed") {
    const claimants = ownership.claimedByPlayerIds.map((playerId) => factionName(playerId, factionNameByPlayerId));

    // A claimed tile with no claimants should not arrive — the server resolves that as wilderness — so the bare label is the
    // honest answer rather than a dangling "Claimed by".
    return claimants.length === 0 ? label : `${label} by ${claimants.join(", ")}`;
  }

  // The holder, where there is one, falling back to the former owner so an abandoned tile still says whose it was. A disputed
  // tile may have nobody holding it at all, and then the status stands alone.
  const holderId = ownership.effectiveHolderPlayerId ?? ownership.lastOwnedByPlayerId;
  const holderName = holderId == null ? null : factionName(holderId, factionNameByPlayerId);

  const parts = [holderName == null ? label : `${label} by ${holderName}`];

  // Rebellion rides alongside the status rather than replacing it, so it reads as the modifier it is.
  if (ownership.isRebelling) {
    parts.push("(rebelling)");
  }

  return parts.join(" ");
}
