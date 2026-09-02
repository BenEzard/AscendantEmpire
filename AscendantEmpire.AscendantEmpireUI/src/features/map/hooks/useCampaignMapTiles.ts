import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  postClearPlayerCampaignMap,
  postPlayerCampaignMap,
  type CampaignMapTilesResponse,
  type ClearPlayerCampaignMapRequest,
  type LoadPlayerCampaignMapRequest,
} from "@/features/map/api/playerCampaignMapApi";

export const playerCampaignTilesKey = (gameId: number | undefined, playerId: number | undefined) => ["playerCampaignTiles", gameId, playerId] as const;

export function usePlayerCampaignMapTilesQuery(gameId: number | undefined, playerId: number | undefined, enabled: boolean) {
  return useQuery<CampaignMapTilesResponse, Error>({
    queryKey: playerCampaignTilesKey(gameId, playerId),
    enabled: enabled && gameId != null && playerId != null,
    refetchOnWindowFocus: false,
    queryFn: async () => {
      if (gameId == null) throw new Error("gameId is null");
      if (playerId == null) throw new Error("playerId is null");
      
      const payload: LoadPlayerCampaignMapRequest = { gameId, playerId, loadWholeMap: true };

      return await postPlayerCampaignMap(payload);
    },
  });
}

/**
 * Folds a freshly observed set of tiles into the ones already cached, leaving untouched tiles exactly as they were.
 *
 * Exported because a force step returns the same shape — the player's view from where the force now stands — and patching that
 * in has to behave identically to a vision request, or the two paths would drift.
 *
 * The two halves of the merge behave differently on purpose, because the two fog layers do. Tiles **accumulate**: what the
 * player has explored is permanent, so a response says only what changed and everything else is left alone. Observers are
 * **replaced** whenever a response carries any: they are the complete picture of what is being watched right now, so ground
 * that has dropped out of the new set is ground nothing is looking at any more, and it has to fall back under haze.
 */
export function mergeTiles(prev: CampaignMapTilesResponse | undefined, next: CampaignMapTilesResponse): CampaignMapTilesResponse {
  if (!prev) return next;

  const merged = prev.tiles.slice();

  for (const t of next.tiles) {
    const idx = merged.findIndex((x) => x.xCoord === t.xCoord && x.yCoord === t.yCoord);
    if (idx >= 0) {
      const existing = merged[idx];

      // `??`, not a length check: an empty array is the API saying the tile is clear, and it has to overwrite what we held.
      // A length check kept the old contents instead, which left a force still drawn on the tile it had just stepped off.
      merged[idx] = {
        ...existing,
        ...t,
        structures: t.structures ?? existing.structures,
        forces: t.forces ?? existing.forces,
      };
    } else {
      merged.push(t);
    }
  }

  return {
    ...prev,
    ...next,
    tiles: merged,
    // `??`, so a response that carries no observers at all leaves the live-sight layer as it was rather than blanking it.
    // Observers used to be stripped out here deliberately, as UI state belonging to the vision dev tool. They are not that
    // any more: they are the live half of the fog of war, and keeping them beside the tiles they describe is what stops the
    // map load and a force step from each holding their own idea of what the player can see.
    observers: next.observers ?? prev.observers,
  };
}

export function useLoadPlayerCampaignMapMutation(opts?: {
  onSuccess?: (data: CampaignMapTilesResponse) => void;
  onError?: (error: unknown) => void;
}) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (req: LoadPlayerCampaignMapRequest) => postPlayerCampaignMap(req),
    onSuccess: (data, variables) => {
      queryClient.setQueryData(
        playerCampaignTilesKey(variables.gameId, variables.playerId),
        (prev: CampaignMapTilesResponse | undefined) => mergeTiles(prev, data),
      );

      opts?.onSuccess?.(data);
    },
    onError: (error) => opts?.onError?.(error),
  });
}

export function useClearPlayerCampaignMapMutation(opts?: {
  onSuccess?: () => void;
  onError?: (error: unknown) => void;
}) {
  const queryClient = useQueryClient();

  return useMutation<void, unknown, ClearPlayerCampaignMapRequest>({
    mutationFn: (req) => postClearPlayerCampaignMap(req),
    onSuccess: (_data, variables) => {
      // Server-side clear may affect any subsequent reads; refresh cached tiles.
      queryClient.invalidateQueries({ queryKey: playerCampaignTilesKey(variables.gameId, variables.playerId) });
      opts?.onSuccess?.();
    },
    onError: (error) => opts?.onError?.(error),
  });
}
