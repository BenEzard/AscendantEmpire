import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  getSplitForceOptions,
  splitForce,
  type SplitForceOptions,
  type SplitForceRequest,
  type SplitForceResult,
} from "@/features/game/api/splitForceApi";
import { playerCampaignTilesKey } from "@/features/map/hooks/useCampaignMapTiles";
import { TILE_SUSTENANCE_KEY_PREFIX } from "@/features/map/hooks/useTileSustenance";
import { TILE_SUPPORT_KEY_PREFIX } from "@/features/map/hooks/useTileSupportLevels";

/** Cache key prefix for the split options, so a force's options can be invalidated without knowing the rest of the key. */
export const SPLIT_FORCE_OPTIONS_KEY_PREFIX = ["splitForceOptions"] as const;

export function splitForceOptionsKey(unitInstanceId: number | null) {
  return [...SPLIT_FORCE_OPTIONS_KEY_PREFIX, unitInstanceId] as const;
}

/**
 * Fetches what a legal division of one force looks like, for the dialog that is about to offer it.
 *
 * Disabled until there is a force to ask about, which is what lets the dialog mount closed without firing a request. Not
 * cached across openings — `staleTime` is left at zero — because the force's strength is exactly the sort of thing that
 * changes between one opening and the next, and a stale quantum count would offer a split the server then refuses.
 */
export function useSplitForceOptionsQuery(unitInstanceId: number | null) {
  return useQuery<SplitForceOptions>({
    queryKey: splitForceOptionsKey(unitInstanceId),
    queryFn: () => getSplitForceOptions(unitInstanceId!),
    enabled: unitInstanceId != null,
    retry: false,
  });
}

/**
 * Performs a split and refreshes everything the division changed.
 *
 * **Refetches** the map rather than patching it, for the same reason founding does: a split adds forces to a tile and reduces
 * the one already there, and the observer patch has no way to express the arrival of several forces at once. The refetch is
 * also what picks up each new force's own sight.
 */
export function useSplitForceMutation(
  gameId: number | undefined,
  playerId: number | undefined,
  opts?: {
    onSuccess?: (data: SplitForceResult) => void;
    onError?: (error: unknown) => void;
  },
) {
  const queryClient = useQueryClient();

  return useMutation<SplitForceResult, unknown, SplitForceRequest>({
    mutationFn: (request) => splitForce(request),
    onSuccess: (data) => {
      // Several forces now stand where one did, and what the player can see may have changed with them.
      queryClient.invalidateQueries({ queryKey: playerCampaignTilesKey(gameId, playerId) });

      // Every cached verdict that judged the original force is wrong: it is smaller, and it has spent its turn. The bare
      // prefix rather than a built key, because each selection is cached separately and this force may sit in several.
      queryClient.invalidateQueries({ queryKey: ["forceContextActions"] });

      // Its own options are stale for the same reason — a second split must be offered against the strength it has left.
      queryClient.invalidateQueries({ queryKey: SPLIT_FORCE_OPTIONS_KEY_PREFIX });

      // The tile now carries more forces drawing on it, though the headcount standing there is unchanged.
      queryClient.invalidateQueries({ queryKey: TILE_SUSTENANCE_KEY_PREFIX });
      queryClient.invalidateQueries({ queryKey: TILE_SUPPORT_KEY_PREFIX });

      opts?.onSuccess?.(data);
    },
    onError: (error) => opts?.onError?.(error),
  });
}

/**
 * How a completed split is announced.
 *
 * Reads out the sizes rather than just the count, because "split into 3" says nothing about whether the player got the
 * division they composed.
 */
export function describeSplit(result: SplitForceResult): string {
  return `Force split into ${result.groups.map((group) => group.size.toLocaleString()).join(" + ")}.`;
}
