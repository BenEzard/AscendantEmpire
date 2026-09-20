import { useQuery } from "@tanstack/react-query";
import { getCampaignPossibleActions, type CampaignPossibleActions } from "@/features/game/api/turnApi";

export const campaignPossibleActionsKey = (gameId: number | undefined, playerId: number | undefined) =>
  ["campaignPossibleActions", gameId, playerId] as const;

/**
 * What the player still has outstanding on the open campaign turn.
 *
 * Read as the game opens so the notifications panel is populated before the player presses anything, and written to again by the end-turn mutation, which
 * reports the same list. `gameId` and `playerId` only scope the cache — the request takes both from the game token.
 *
 * Not refetched on window focus, unlike the intelligence feed. This list is about the player's own forces, which cannot move while the tab is in the
 * background, and a refetch would discard the list the end-turn check just wrote into the cache.
 *
 * Caller-gated rather than self-gating: the caller knows whose turn it is, and there is no point asking the server a question whose answer it already has.
 */
export function useCampaignPossibleActionsQuery(gameId: number | undefined, playerId: number | undefined, enabled: boolean) {
  return useQuery<CampaignPossibleActions, Error>({
    queryKey: campaignPossibleActionsKey(gameId, playerId),
    enabled: enabled && gameId != null && playerId != null,
    refetchOnWindowFocus: false,
    queryFn: () => getCampaignPossibleActions(),
  });
}
