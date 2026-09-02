import { useMutation, useQueryClient } from "@tanstack/react-query";
import { postEndCampaignTurn, type EndCampaignTurnOutcome } from "@/features/game/api/turnApi";
import { playerCampaignTilesKey } from "@/features/map/hooks/useCampaignMapTiles";
import { unitDisplayInfoKey } from "@/features/map/hooks/useUnitDisplayInfo";

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
      }

      opts?.onSuccess?.(outcome);
    },
    onError: (error) => opts?.onError?.(error),
  });
}
