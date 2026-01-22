import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  postClearPlayerCampaignMap,
  postPlayerCampaignMap,
  type CampaignMapTilesResponse,
  type ClearPlayerCampaignMapRequest,
  type LoadPlayerCampaignMapRequest,
} from "@/features/map/api/playerCampaignMapApi";

export const playerCampaignTilesKey = (gameGuid: string | undefined, playerGuid: string | undefined) =>
  ["playerCampaignTiles", gameGuid, playerGuid] as const;

export function usePlayerCampaignMapTilesQuery(gameGuid: string | undefined, playerGuid: string | undefined, enabled: boolean) {
  return useQuery<CampaignMapTilesResponse, Error>({
    queryKey: playerCampaignTilesKey(gameGuid, playerGuid),
    enabled: enabled && !!gameGuid && !!playerGuid,
    refetchOnWindowFocus: false,
    queryFn: async () => {
      if (gameGuid == null) throw new Error("gameGuid is null");
      if (playerGuid == null) throw new Error("playerGuid is null");
      
      const payload: LoadPlayerCampaignMapRequest = { gameGuid, playerGuid, loadWholeMap: true };

      return await postPlayerCampaignMap(payload);
    },
  });
}

function mergeTiles(prev: CampaignMapTilesResponse | undefined, next: CampaignMapTilesResponse): CampaignMapTilesResponse {
  if (!prev) return next;

  const merged = prev.tiles.slice();

  for (const t of next.tiles) {
    const idx = merged.findIndex((x) => x.xCoord === t.xCoord && x.yCoord === t.yCoord);
    if (idx >= 0) merged[idx] = t;
    else merged.push(t);
  }

  return {
    // Intentionally do NOT merge/persist observer results into the tiles cache.
    // Vision observer data is UI-tool state and should be kept separate.
    ...prev,
    ...(() => {
      const { observers: _observers, ...rest } = next;
      return rest;
    })(),
    tiles: merged,
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
        playerCampaignTilesKey(variables.gameGuid, variables.playerGuid),
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
      queryClient.invalidateQueries({ queryKey: playerCampaignTilesKey(variables.gameGuid, variables.playerGuid) });
      opts?.onSuccess?.();
    },
    onError: (error) => opts?.onError?.(error),
  });
}
