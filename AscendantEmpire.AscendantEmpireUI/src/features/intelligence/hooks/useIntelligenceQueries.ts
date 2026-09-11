import { useQuery } from "@tanstack/react-query";
import { getKnownForces, getSightingReports, type KnownForce, type SightingReport } from "@/features/intelligence/api/intelligenceApi";

export const sightingReportsKey = (gameId: number | undefined, playerId: number | undefined) => ["sightingReports", gameId, playerId] as const;

export const knownForcesKey = (gameId: number | undefined, playerId: number | undefined) => ["knownForces", gameId, playerId] as const;

/**
 * The player's intelligence feed: reports raised since their last turn ended.
 *
 * Refetched on window focus, unlike the map. Reports are raised when *other* players end their turns, so in an asynchronous game new intelligence arrives while
 * this tab sits in the background, and coming back to it is exactly when the player wants to see it. `gameId` and `playerId` only scope the cache — the request
 * takes both from the game token.
 */
export function useSightingReportsQuery(gameId: number | undefined, playerId: number | undefined, enabled: boolean) {
  return useQuery<SightingReport[], Error>({
    queryKey: sightingReportsKey(gameId, playerId),
    enabled: enabled && gameId != null && playerId != null,
    refetchOnWindowFocus: true,
    queryFn: () => getSightingReports(),
  });
}

/** Every force the player has watched, as last seen. Refetched on focus for the same reason as the feed. */
export function useKnownForcesQuery(gameId: number | undefined, playerId: number | undefined, enabled: boolean) {
  return useQuery<KnownForce[], Error>({
    queryKey: knownForcesKey(gameId, playerId),
    enabled: enabled && gameId != null && playerId != null,
    refetchOnWindowFocus: true,
    queryFn: () => getKnownForces(),
  });
}
