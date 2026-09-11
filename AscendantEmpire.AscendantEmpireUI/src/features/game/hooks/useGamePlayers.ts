import { useQuery } from "@tanstack/react-query";
import { getGamePlayers, type GamePlayer } from "@/features/game/api/gamePlayersApi";

export const gamePlayersKey = (gameId: number | undefined) => ["gamePlayers", gameId] as const;

/**
 * Every player in the entered game.
 *
 * Changes rarely — a claimed slot, a renamed faction — so it is not refetched on focus. `gameId` is only the cache key: the request takes the game from the token.
 */
export function useGamePlayersQuery(gameId: number | undefined, enabled: boolean) {
  return useQuery<GamePlayer[], Error>({
    queryKey: gamePlayersKey(gameId),
    enabled: enabled && gameId != null,
    refetchOnWindowFocus: false,
    queryFn: () => getGamePlayers(),
  });
}
