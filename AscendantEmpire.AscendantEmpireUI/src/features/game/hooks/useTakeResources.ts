import { useMutation, useQuery, useQueryClient, type QueryClient } from "@tanstack/react-query";
import {
  getTakeResourcesOptions,
  takeResources,
  type TakeResourcesOptions,
  type TakeResourcesRequest,
  type TakeResourcesResult,
} from "@/features/game/api/takeResourcesApi";
import { playerCampaignTilesKey } from "@/features/map/hooks/useCampaignMapTiles";
import { TILE_SUSTENANCE_KEY_PREFIX } from "@/features/map/hooks/useTileSustenance";
import { TILE_SUPPORT_KEY_PREFIX } from "@/features/map/hooks/useTileSupportLevels";
import { playerMapResourcesKey } from "@/features/map/hooks/usePlayerMapResources";

/** Cache key prefix for the take options, so they can be invalidated without knowing which selection asked. */
export const TAKE_RESOURCES_OPTIONS_KEY_PREFIX = ["takeResourcesOptions"] as const;

export function takeResourcesOptionsKey(forceIds: readonly number[] | null) {
  return [...TAKE_RESOURCES_OPTIONS_KEY_PREFIX, forceIds?.join(",") ?? null] as const;
}

/**
 * Fetches what the selection could take off the tile it stands on, for the dialog about to offer the choice.
 *
 * Disabled until there is a selection, which is what lets the dialog mount closed without firing a request. Not cached across
 * openings — `staleTime` is left at zero — because what the tile holds falls as anyone takes from it, and a stale figure would
 * promise a gain the server then cannot deliver.
 */
export function useTakeResourcesOptionsQuery(forceIds: readonly number[] | null) {
  return useQuery<TakeResourcesOptions>({
    queryKey: takeResourcesOptionsKey(forceIds),
    queryFn: () => getTakeResourcesOptions(forceIds!),
    enabled: forceIds != null && forceIds.length > 0,
    retry: false,
  });
}

/**
 * Reads the options once, outside a render, for the caller that has to know how many resources there are before it can decide
 * whether to ask the player anything at all.
 *
 * Against the same cache entry the query above uses, so the dialog it may go on to open finds the answer already there rather
 * than asking the server a second time for what was just read.
 */
export function fetchTakeResourcesOptions(queryClient: QueryClient, forceIds: readonly number[]): Promise<TakeResourcesOptions> {
  return queryClient.fetchQuery<TakeResourcesOptions>({
    queryKey: takeResourcesOptionsKey(forceIds),
    queryFn: () => getTakeResourcesOptions(forceIds),
    retry: false,
  });
}

/**
 * Takes a map resource into cargo and refreshes everything the take changed.
 *
 * **Refetches** the map rather than patching it, for the reason founding does: a quantity moved off the tile and into a bank,
 * and the observer patch has no way to express either half. The map resource estimates go with it, since the tilde figure the
 * player sees is derived from the quantity that just fell, and the tile's food with them where the resource was edible.
 */
export function useTakeResourcesMutation(
  gameId: number | undefined,
  playerId: number | undefined,
  opts?: {
    onSuccess?: (data: TakeResourcesResult) => void;
    onError?: (error: unknown) => void;
  },
) {
  const queryClient = useQueryClient();

  return useMutation<TakeResourcesResult, unknown, TakeResourcesRequest>({
    mutationFn: (request) => takeResources(request),
    onSuccess: (data) => {
      // The tile's resource has shrunk, and its forces are carrying something they were not.
      queryClient.invalidateQueries({ queryKey: playerCampaignTilesKey(gameId, playerId) });

      // Every taking force has spent its turn, so each cached verdict that judged it is wrong. The bare prefix rather than a
      // built key, because each selection is cached separately and these forces may sit in several.
      queryClient.invalidateQueries({ queryKey: ["forceContextActions"] });

      // A second take must be offered against what is left, which may now be nothing.
      queryClient.invalidateQueries({ queryKey: TAKE_RESOURCES_OPTIONS_KEY_PREFIX });

      // Where the resource was edible, the patches standing forces eat have just been carried off.
      queryClient.invalidateQueries({ queryKey: TILE_SUSTENANCE_KEY_PREFIX });
      queryClient.invalidateQueries({ queryKey: TILE_SUPPORT_KEY_PREFIX });

      // The player's estimate of the tile's resource is derived from the quantity this order reduced.
      queryClient.invalidateQueries({ queryKey: playerMapResourcesKey(gameId, playerId) });

      opts?.onSuccess?.(data);
    },
    onError: (error) => opts?.onError?.(error),
  });
}

/**
 * How a completed take is announced.
 *
 * Reads out the assets rather than the raw units, because units are an internal measure — what the player cares about is what
 * is now in the hold. Says so when the resource ran out, since the button will be gone on the next look.
 */
export function describeTake(result: TakeResourcesResult): string {
  const gained = result.gained
    .map((line) => `${line.amount.toLocaleString()} ${line.assetDisplayName}`)
    .join(", ");

  const haul = gained.length > 0 ? `Took ${gained} from the ${result.displayName}.` : `Took nothing from the ${result.displayName}.`;

  return result.isExhausted ? `${haul} It is now exhausted.` : haul;
}
