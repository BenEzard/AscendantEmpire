import { useQuery } from "@tanstack/react-query";
import { getPlayerMapResources, type PlayerMapResource } from "../api/playerMapResourceApi";

export const playerMapResourcesKey = (gameId: number | undefined, playerId: number | undefined) => ["playerMapResources", gameId, playerId] as const;

/**
 * Every MapResource the player has discovered, for drawing on the map tiles.
 *
 * Read once for the whole map, unlike sustenance: a tile's resources are drawn on every tile at once, so asking per tile would be a request per tile.
 *
 * Refetched on window focus, like the intelligence feed, and invalidated whenever sight or quantities can have moved — a turn ending, a force stepping,
 * settling or splitting. Sight is what decides whether an entry carries a quantity at all, and the server derives it when the read is served, so a force
 * walking up to a known patch has to re-read before its estimate appears.
 *
 * `gameId` and `playerId` only scope the cache — the request takes both from the game token.
 */
export function usePlayerMapResourcesQuery(gameId: number | undefined, playerId: number | undefined, enabled: boolean) {
  return useQuery<PlayerMapResource[], Error>({
    queryKey: playerMapResourcesKey(gameId, playerId),
    enabled: enabled && gameId != null && playerId != null,
    refetchOnWindowFocus: true,
    queryFn: () => getPlayerMapResources(),
  });
}
