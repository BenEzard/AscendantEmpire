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
      
      const payload: LoadPlayerCampaignMapRequest = { gameId, playerId, loadWholeMap: true, observers: null };

      return await postPlayerCampaignMap(payload);
    },
  });
}

function mergeTiles(prev: CampaignMapTilesResponse | undefined, next: CampaignMapTilesResponse): CampaignMapTilesResponse {
  if (!prev) return next;

  const merged = prev.tiles.slice();

  for (const t of next.tiles) {
    const idx = merged.findIndex((x) => x.xCoord === t.xCoord && x.yCoord === t.yCoord);
    if (idx >= 0) {
      const existing = merged[idx];

      merged[idx] = {
        ...existing,
        ...t,
        structures: t.structures && t.structures.length > 0 ? t.structures : existing.structures,
        troops: t.troops && t.troops.length > 0 ? t.troops : existing.troops,
      };
    } else {
      merged.push(t);
    }
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
