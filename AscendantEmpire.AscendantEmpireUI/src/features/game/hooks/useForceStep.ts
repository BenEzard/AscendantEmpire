import { useMutation, useQueryClient } from "@tanstack/react-query";
import { postStepForce, type StepForceRequest, type StepForceResponse } from "@/features/game/api/forceMovementApi";
import { mergeTiles, playerCampaignTilesKey } from "@/features/map/hooks/useCampaignMapTiles";
import type { CampaignMapTilesResponse } from "@/features/map/api/playerCampaignMapApi";

/**
 * Steps a single force one tile and folds the result back into the cached map.
 *
 * The response carries the player's refreshed view along with the move, so this patches the tiles the server actually reported
 * — the one the force left, the one it arrived on, and anything newly revealed — rather than refetching the whole map. Every
 * other tile keeps its object identity, which is what lets the memoised grid leave them alone.
 */
export function useForceStepMutation(
  gameId: number | undefined,
  playerId: number | undefined,
  opts?: {
    onSuccess?: (data: StepForceResponse) => void;
    onError?: (error: unknown) => void;
  },
) {
  const queryClient = useQueryClient();

  return useMutation<StepForceResponse, unknown, StepForceRequest>({
    mutationFn: (req) => postStepForce(req),
    onSuccess: (data) => {
      queryClient.setQueryData(
        playerCampaignTilesKey(gameId, playerId),
        (prev: CampaignMapTilesResponse | undefined) => mergeTiles(prev, { tiles: data.tiles }),
      );

      // The toolbar's verdicts are judged against movement and strength, so a step invalidates them — see the note on this key
      // in useForceContextActions. The bare prefix rather than a built key: each selection is cached separately, and the moved
      // force may sit in several of those entries.
      queryClient.invalidateQueries({ queryKey: ["forceContextActions"] });

      opts?.onSuccess?.(data);
    },
    onError: (error) => opts?.onError?.(error),
  });
}
