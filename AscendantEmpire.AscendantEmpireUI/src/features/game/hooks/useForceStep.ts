import { useMutation, useQueryClient } from "@tanstack/react-query";
import { postStepForce, type StepForceRequest, type StepForceResponse } from "@/features/game/api/forceMovementApi";
import { mergeTiles, playerCampaignTilesKey } from "@/features/map/hooks/useCampaignMapTiles";
import type { CampaignMapTilesResponse } from "@/features/map/api/playerCampaignMapApi";
import { TILE_SUSTENANCE_KEY_PREFIX } from "@/features/map/hooks/useTileSustenance";

/**
 * Steps a single force one tile and folds the result back into the cached map.
 *
 * The response carries the player's refreshed view along with the move, so this patches the tiles the server actually reported
 * — the one the force left, the one it arrived on, and anything newly revealed — rather than refetching the whole map. Every
 * other tile keeps its object identity, which is what lets the memoised grid leave them alone.
 *
 * The observers go into the cache alongside the tiles, because they are what the fog is drawn from: the step has moved one of
 * them, so the ground it now watches has to be lit and the ground behind it dimmed back to explored-but-unwatched.
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
        (prev: CampaignMapTilesResponse | undefined) => mergeTiles(prev, { tiles: data.tiles, observers: data.observers }),
      );

      // The toolbar's verdicts are judged against movement and strength, so a step invalidates them — see the note on this key
      // in useForceContextActions. The bare prefix rather than a built key: each selection is cached separately, and the moved
      // force may sit in several of those entries.
      queryClient.invalidateQueries({ queryKey: ["forceContextActions"] });

      // A step moves one of the player's observers, so a tile can come into or drop out of sight, and the server only discloses sustenance on tiles in
      // sight. Stepping feeds nobody, so the amounts themselves don't change; only the query the tile bar is watching actually refetches.
      queryClient.invalidateQueries({ queryKey: TILE_SUSTENANCE_KEY_PREFIX });

      opts?.onSuccess?.(data);
    },
    onError: (error) => opts?.onError?.(error),
  });
}
