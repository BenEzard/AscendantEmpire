import { useQuery } from "@tanstack/react-query";
import { getPlayerResearch, type PlayerResearchOverview } from "@/features/game/advisors/science/scienceApi";

export const playerResearchKey = (gameId: number | undefined, playerId: number | undefined) => ["playerResearch", gameId, playerId] as const;

/**
 * Where the Player's research stands: their current project, their banked points, and what they could research now.
 *
 * Refetched on focus, because in an asynchronous game the Round advances while the tab sits in the background, and a
 * technology another player's turn made available would otherwise stay hidden until the dialog was reopened.
 *
 * Not retried: the API answers 403 when no game has been entered and 404 when the Player has no research state, both of
 * which are ordinary states whose right answer is an empty list rather than a retry. `gameId` and `playerId` only scope
 * the cache — the request takes both from the game token, which is the only trustworthy answer to "which player am I".
 */
export function usePlayerResearchQuery(gameId: number | undefined, playerId: number | undefined, enabled: boolean) {
  return useQuery<PlayerResearchOverview, Error>({
    queryKey: playerResearchKey(gameId, playerId),
    enabled: enabled && gameId != null && playerId != null,
    retry: false,
    refetchOnWindowFocus: true,
    queryFn: ({ signal }) => getPlayerResearch(signal),
  });
}
