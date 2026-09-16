import { http } from "@/api/http";
import { parseTileSupportLevel, type TileSupportLevel } from "./tileSustenanceApi";

/** One observed tile's support level for the selected forces. */
export interface TileSupportLevelEntry {
  x: number;
  y: number;
  supportLevel: TileSupportLevel;
}

/** The response as it arrives. Every field is optional on the wire, so a missing one is read as absent rather than trusted. */
type TileSupportLevelsWireDto = {
  tiles?: { x?: number; y?: number; supportLevel?: string | null }[] | null;
};

/**
 * The support level of every tile in the player's live sight, for the given forces. A tile's need is those forces plus the player's own forces already standing on it.
 *
 * Tiles out of sight, and tiles with food but nobody to feed, are left out by the server. The game and player come from the game token, not the request.
 */
export async function getTileSupportLevels(forceIds: readonly number[]): Promise<TileSupportLevelEntry[]> {
  // Repeated rather than comma-joined, which is how ASP.NET binds an int[] from the query string: ?forceIds=1&forceIds=2.
  const query = new URLSearchParams();
  for (const forceId of forceIds) {
    query.append("forceIds", String(forceId));
  }

  const { data } = await http.get<TileSupportLevelsWireDto>(`/campaign-map/player/tiles/support?${query.toString()}`);

  const entries: TileSupportLevelEntry[] = [];

  for (const tile of data.tiles ?? []) {
    const supportLevel = parseTileSupportLevel(tile.supportLevel);

    // An entry missing its coordinates or carrying a level this client doesn't know has nowhere sensible to be drawn, so it is dropped.
    if (tile.x == null || tile.y == null || supportLevel == null) {
      continue;
    }

    entries.push({ x: tile.x, y: tile.y, supportLevel });
  }

  return entries;
}
