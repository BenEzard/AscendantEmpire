import { useQuery } from "@tanstack/react-query";
import { getTileSupportLevels } from "../api/tileSupportApi";
import type { TileSupportLevel } from "../api/tileSustenanceApi";

/** The prefix every selection's support levels are cached under, for invalidating all of them at once. */
export const TILE_SUPPORT_KEY_PREFIX = ["tileSupport"] as const;

export const tileSupportLevelsKey = (gameId: number | undefined, playerId: number | undefined, sortedForceIds: readonly number[]) =>
  [...TILE_SUPPORT_KEY_PREFIX, gameId, playerId, ...sortedForceIds] as const;

/**
 * The support level of every tile in the player's live sight for the selected forces, keyed by `"x,y"`, for the hover marker.
 *
 * Asked for once per selection rather than once per hovered tile. Every tile read has to work out the player's sight on the server, which is the expensive
 * part, so one request covering every observed tile keeps hovering down to a map lookup — the marker is instant, and moving the pointer costs nothing.
 *
 * Pass the selection's ids already sorted, so the cache key is stable however the player reordered the selection. Disabled while nothing is selected.
 *
 * Invalidated (by prefix) when this player's turn ends — forces feed — and when a force steps, since a step changes both which tiles are in sight and where
 * the selected force stands. Refetched on window focus for the same reason as the tile sustenance: other players feed while this tab sits in the background.
 * `gameId` and `playerId` only scope the cache — the request takes both from the game token.
 */
export function useTileSupportLevelsQuery(gameId: number | undefined, playerId: number | undefined, sortedForceIds: readonly number[]) {
  return useQuery<ReadonlyMap<string, TileSupportLevel>, Error>({
    queryKey: tileSupportLevelsKey(gameId, playerId, sortedForceIds),
    enabled: gameId != null && playerId != null && sortedForceIds.length > 0,
    refetchOnWindowFocus: true,
    queryFn: async () => {
      const entries = await getTileSupportLevels(sortedForceIds);

      return new Map(entries.map((entry) => [`${entry.x},${entry.y}`, entry.supportLevel]));
    },
  });
}
