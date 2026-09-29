import { useQuery } from "@tanstack/react-query";
import { getPlayerResources, type PlayerResourceMetaCategory } from "@/features/game/api/playerResourcesApi";

export const playerResourcesKey = (gameId: number | undefined, playerId: number | undefined) => ["playerResources", gameId, playerId] as const;

/**
 * The Player's unlocked resource tree: the meta-categories the Resources toolbar shows, and everything their panels render.
 *
 * Refetched on focus, because in an asynchronous game the Round advances while the tab sits in the background, and both the amounts and the
 * obsoleting countdowns are measured against it. Invalidated by the end-turn mutation for the same reason.
 *
 * Not retried: the API answers 404 when the Player is not in an in-progress game or it has no turn queue, which is an ordinary state whose right
 * answer is an empty Resources group. `gameId` and `playerId` only scope the cache — the request takes both from the game token.
 */
export function usePlayerResourcesQuery(gameId: number | undefined, playerId: number | undefined, enabled: boolean) {
  return useQuery<PlayerResourceMetaCategory[], Error>({
    queryKey: playerResourcesKey(gameId, playerId),
    enabled: enabled && gameId != null && playerId != null,
    retry: false,
    refetchOnWindowFocus: true,
    queryFn: () => getPlayerResources(),
  });
}
