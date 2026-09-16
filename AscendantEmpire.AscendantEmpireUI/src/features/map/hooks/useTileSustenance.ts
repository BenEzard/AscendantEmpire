import { useQuery } from "@tanstack/react-query";
import { getTileSustenance, type TileSustenance } from "../api/tileSustenanceApi";

/** The prefix every tile's sustenance is cached under, for invalidating all of them at once. */
export const TILE_SUSTENANCE_KEY_PREFIX = ["tileSustenance"] as const;

export const tileSustenanceKey = (gameId: number | undefined, playerId: number | undefined, x: number | undefined, y: number | undefined) =>
  [...TILE_SUSTENANCE_KEY_PREFIX, gameId, playerId, x, y] as const;

/**
 * What is available to the player on one tile, for the tile toolbar.
 *
 * Fetched per tile, on selection, rather than for the whole map: the amounts only change when a turn ends, and the player looks at one tile at a time, so
 * each tile is asked for once and cached. Clicking back to a tile already asked about costs nothing.
 *
 * Pass `enabled` only for a tile in live sight. The server discloses nothing about any other tile, so asking would be a wasted round trip.
 *
 * Refetched on window focus, like the intelligence feed: other players end their turns — and feed on tiles — while this tab sits in the background.
 * Invalidated (by prefix) when this player's turn ends and when a force steps, since a step changes which tiles are in sight. `gameId` and `playerId`
 * only scope the cache — the request takes both from the game token.
 */
export function useTileSustenanceQuery(
  gameId: number | undefined,
  playerId: number | undefined,
  coords: { x: number; y: number } | null,
  enabled: boolean,
) {
  return useQuery<TileSustenance, Error>({
    queryKey: tileSustenanceKey(gameId, playerId, coords?.x, coords?.y),
    enabled: enabled && gameId != null && playerId != null && coords != null,
    refetchOnWindowFocus: true,
    queryFn: () => getTileSustenance(coords!.x, coords!.y),
  });
}
