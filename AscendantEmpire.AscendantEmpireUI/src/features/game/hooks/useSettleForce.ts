import { useMutation, useQueryClient } from "@tanstack/react-query";
import { settleForce, type SettleForceRequest, type SettleForceResult } from "@/features/game/api/settleForceApi";
import { playerCampaignTilesKey } from "@/features/map/hooks/useCampaignMapTiles";
import { TILE_SUSTENANCE_KEY_PREFIX } from "@/features/map/hooks/useTileSustenance";
import { TILE_SUPPORT_KEY_PREFIX } from "@/features/map/hooks/useTileSupportLevels";

/**
 * Founds a settlement, consuming the settler, and refreshes everything the founding changed.
 *
 * Unlike `useForceStepMutation` this **refetches** the map rather than patching it. A step moves a force and the response
 * carries the tiles it now sees, so the cache can be merged; founding removes a force and adds a structure in the same breath,
 * and the merge has no way to express a removal — a patched tile would keep drawing the settler that is no longer there. The
 * refetch is also the only way to pick up the structure's own sight, which may reach further than the force's did.
 */
/**
 * How a founding is announced, wherever it was started from.
 *
 * Shared because a village is founded through the naming dialog and a camp straight off the toolbar button, and the two
 * should not drift into reporting the same event differently. A camp has no name, so it is described by its tier.
 */
export function describeFounding(result: SettleForceResult): string {
  const what = result.name ?? `Your ${result.tier.toLowerCase()}`;
  const turns = `${result.turnsToComplete} turn${result.turnsToComplete === 1 ? "" : "s"}`;

  return `${what} founded with ${result.population} people. Ready in ${turns}.`;
}

export function useSettleForceMutation(
  gameId: number | undefined,
  playerId: number | undefined,
  opts?: {
    onSuccess?: (data: SettleForceResult) => void;
    onError?: (error: unknown) => void;
  },
) {
  const queryClient = useQueryClient();

  return useMutation<SettleForceResult, unknown, SettleForceRequest>({
    mutationFn: (request) => settleForce(request),
    onSuccess: (data) => {
      // The whole view: a force has gone, a structure has appeared, and what the player can see may have changed with it.
      queryClient.invalidateQueries({ queryKey: playerCampaignTilesKey(gameId, playerId) });

      // The settler no longer exists, so every cached verdict that judged it is wrong. The bare prefix rather than a built key:
      // each selection is cached separately, and the consumed force may sit in several of those entries.
      queryClient.invalidateQueries({ queryKey: ["forceContextActions"] });

      // A force left the tile, so what the tile can support, and what is drawing on it, have both changed.
      queryClient.invalidateQueries({ queryKey: TILE_SUSTENANCE_KEY_PREFIX });
      queryClient.invalidateQueries({ queryKey: TILE_SUPPORT_KEY_PREFIX });

      opts?.onSuccess?.(data);
    },
    onError: (error) => opts?.onError?.(error),
  });
}
