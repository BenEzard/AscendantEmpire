import { useQuery } from "@tanstack/react-query";
import { getCurrentCampaignTurn, type CurrentCampaignTurn } from "@/features/game/api/turnApi";

export const currentCampaignTurnKey = (gameId: number | undefined) => ["currentCampaignTurn", gameId] as const;

/**
 * Whose campaign turn it is, and which round.
 *
 * Not retried: the API answers 404 when nobody is taking a turn, which is an ordinary state, and the callers — ageing known-force markers — simply treat it as
 * "round unknown". Refetched on focus, because in an asynchronous game the round moves on while the tab sits in the background.
 */
export function useCurrentCampaignTurnQuery(gameId: number | undefined, enabled: boolean) {
  return useQuery<CurrentCampaignTurn, Error>({
    queryKey: currentCampaignTurnKey(gameId),
    enabled: enabled && gameId != null,
    retry: false,
    refetchOnWindowFocus: true,
    queryFn: () => getCurrentCampaignTurn(),
  });
}
