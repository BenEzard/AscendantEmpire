import { useMutation, useQueryClient } from "@tanstack/react-query";
import { postEndCampaignTurn, type EndCampaignTurnOutcome } from "@/features/game/api/turnApi";
import { playerCampaignTilesKey } from "@/features/map/hooks/useCampaignMapTiles";
import { unitDisplayInfoKey } from "@/features/map/hooks/useUnitDisplayInfo";
import { knownForcesKey, sightingReportsKey } from "@/features/intelligence/hooks/useIntelligenceQueries";
import { currentCampaignTurnKey } from "./useCurrentCampaignTurn";

/**
 * Ends the current player's campaign turn.
 *
 * A turn that actually ends changes more of the world than a single response can carry — the end-of-turn processes run, movement is restored, and the
 * map may have moved on under someone else's turn — so this drops the cached map and unit state and lets it refetch, rather than trying to patch it
 * the way a single force step does.
 *
 * A call that comes back with `turnEnded` false has changed nothing: the server only reported what the player still has outstanding, so the caches
 * are left exactly as they were.
 */
export function useEndTurnMutation(
  gameId: number | undefined,
  playerId: number | undefined,
  opts?: {
    onSuccess?: (outcome: EndCampaignTurnOutcome) => void;
    onError?: (error: unknown) => void;
  },
) {
  const queryClient = useQueryClient();

  return useMutation<EndCampaignTurnOutcome, unknown, boolean>({
    mutationFn: (force) => postEndCampaignTurn(force),
    onSuccess: (outcome) => {
      if (outcome.turnEnded) {
        queryClient.invalidateQueries({ queryKey: playerCampaignTilesKey(gameId, playerId) });
        queryClient.invalidateQueries({ queryKey: unitDisplayInfoKey(gameId) });

        // The toolbar's verdicts are judged against movement and strength, both of which the end of a turn can change. The bare prefix rather than a
        // built key: each selection is cached separately, and any of those entries may hold a force the turn's processes touched.
        queryClient.invalidateQueries({ queryKey: ["forceContextActions"] });

        // The feed's window is measured from the player's last ended turn, which has just moved, and the round may have moved with it. Their own sightings
        // step raised nothing for them, but a feed read before now still holds the reports from before this turn, and those are no longer new.
        queryClient.invalidateQueries({ queryKey: sightingReportsKey(gameId, playerId) });
        queryClient.invalidateQueries({ queryKey: knownForcesKey(gameId, playerId) });
        queryClient.invalidateQueries({ queryKey: currentCampaignTurnKey(gameId) });
      }

      opts?.onSuccess?.(outcome);
    },
    onError: (error) => opts?.onError?.(error),
  });
}
