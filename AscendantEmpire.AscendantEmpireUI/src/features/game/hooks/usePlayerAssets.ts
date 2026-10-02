import { useQuery } from "@tanstack/react-query";
import { getPlayerAssets, type PlayerAssetMetaCategory } from "@/features/game/api/playerAssetsApi";

export const playerAssetsKey = (gameId: number | undefined, playerId: number | undefined) => ["playerAssets", gameId, playerId] as const;

/**
 * The Player's unlocked asset tree: the meta-categories the Assets toolbar shows, and everything their panels render.
 *
 * Refetched on focus, because in an asynchronous game the Round advances while the tab sits in the background, and both the amounts and the
 * obsoleting countdowns are measured against it. Invalidated by the end-turn mutation for the same reason.
 *
 * Not retried: the API answers 404 when the Player is not in an in-progress game or it has no turn queue, which is an ordinary state whose right
 * answer is an empty Assets group. `gameId` and `playerId` only scope the cache — the request takes both from the game token.
 */
export function usePlayerAssetsQuery(gameId: number | undefined, playerId: number | undefined, enabled: boolean) {
  return useQuery<PlayerAssetMetaCategory[], Error>({
    queryKey: playerAssetsKey(gameId, playerId),
    enabled: enabled && gameId != null && playerId != null,
    retry: false,
    refetchOnWindowFocus: true,
    queryFn: () => getPlayerAssets(),
  });
}
