import { http } from "@/api/http";
import { parseTileSupportLevel, type TileSupportLevel } from "./tileSustenanceApi";

/** How many turns ahead the server forecasts. A tile that still feeds the forces after this many comes back with `turns: null`. */
export const SUPPORT_FORECAST_TURNS = 5;

/** How well one tile can feed the selected forces: the level for the colour, and the turns for the number in the marker. */
export interface TileSupport {
  supportLevel: TileSupportLevel;

  /**
   * The whole turns the tile can feed the forces, with the tile regrowing at the end of each round: 0 up to `SUPPORT_FORECAST_TURNS`.
   *
   * - `null`: it still feeds them after the forecast's last turn — possibly for good.
   * - `undefined`: the server didn't say, so no number is shown.
   */
  turns: number | null | undefined;
}

/** One observed tile's support for the selected forces. */
export interface TileSupportLevelEntry extends TileSupport {
  x: number;
  y: number;
}

/** The response as it arrives. Every field is optional on the wire, so a missing one is read as absent rather than trusted. */
type TileSupportLevelsWireDto = {
  tiles?: { x?: number; y?: number; supportLevel?: string | null; turns?: number | null }[] | null;
};

/** Reads the turns off the wire: a whole number, null for beyond the forecast, and anything else — missing or malformed — as not said. */
function parseSupportTurns(value: unknown): number | null | undefined {
  if (value === null) {
    return null;
  }

  return typeof value === "number" && Number.isInteger(value) && value >= 0 ? value : undefined;
}

/**
 * The support level, and the turns forecast, of every tile in the player's live sight for the given forces. A tile's need is those forces plus the player's own
 * forces already standing on it.
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

    entries.push({ x: tile.x, y: tile.y, supportLevel, turns: parseSupportTurns(tile.turns) });
  }

  return entries;
}
