import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useRef } from "react";
import {
  getCampaignMapStartPositions,
  putCampaignMapStartPositions,
  type StartPositionDto,
  type StartPositionsResponse,
} from "../api/startPositionsApi";

// ---------------------------------------------------------------------------
// Query key
// ---------------------------------------------------------------------------

export const mapGalleryStartPositionsKey = (campaignMapGalleryIndexId: number | null) =>
  ["mapGalleryStartPositions", campaignMapGalleryIndexId] as const;

// ---------------------------------------------------------------------------
// Query: load start positions when a map is open
// ---------------------------------------------------------------------------

export function useCampaignMapStartPositionsQuery(
  campaignMapGalleryIndexId: number | null,
  enabled: boolean,
) {
  return useQuery<StartPositionsResponse, Error>({
    queryKey: mapGalleryStartPositionsKey(campaignMapGalleryIndexId),
    enabled: enabled && campaignMapGalleryIndexId != null,
    refetchOnWindowFocus: false,
    queryFn: async ({ signal }) => {
      if (campaignMapGalleryIndexId == null) throw new Error("campaignMapGalleryIndexId is null");
      return getCampaignMapStartPositions(campaignMapGalleryIndexId, signal);
    },
  });
}

// ---------------------------------------------------------------------------
// Mutation: PUT (full-replace) start positions; optimistic update + rollback
// ---------------------------------------------------------------------------

export type PutStartPositionsVariables = {
  campaignMapGalleryIndexId: number;
  /** The complete updated collection to persist. */
  startPositions: StartPositionDto[];
};

type MutationContext = {
  campaignMapGalleryIndexId: number;
  key: ReturnType<typeof mapGalleryStartPositionsKey>;
};

export function usePutCampaignMapStartPositionsMutation(opts?: {
  onFailureToast?: (message: string) => void;
}) {
  const queryClient = useQueryClient();
  const rollbackStateRef = useRef<Map<number, { pendingCount: number; base?: StartPositionsResponse }>>(new Map());

  return useMutation<StartPositionsResponse, Error, PutStartPositionsVariables, MutationContext>({
    mutationFn: async (vars) =>
      putCampaignMapStartPositions(vars.campaignMapGalleryIndexId, {
        startPositions: vars.startPositions,
      }),

    onMutate: async (vars): Promise<MutationContext> => {
      const key = mapGalleryStartPositionsKey(vars.campaignMapGalleryIndexId);

      await queryClient.cancelQueries({ queryKey: key });

      const existingRollbackState = rollbackStateRef.current.get(vars.campaignMapGalleryIndexId);
      if (existingRollbackState == null) {
        rollbackStateRef.current.set(vars.campaignMapGalleryIndexId, {
          pendingCount: 1,
          base: queryClient.getQueryData<StartPositionsResponse>(key),
        });
      } else {
        rollbackStateRef.current.set(vars.campaignMapGalleryIndexId, {
          ...existingRollbackState,
          pendingCount: existingRollbackState.pendingCount + 1,
        });
      }

      // Optimistically apply the new collection (assign temporary id = 0 since the
      // real IDs come back from the server on success).
      queryClient.setQueryData<StartPositionsResponse>(key, (old) => {
        const base = old ?? { campaignMapGalleryIndexId: vars.campaignMapGalleryIndexId, startPositions: [] };
        return {
          ...base,
          startPositions: vars.startPositions.map((sp, i) => ({
            id: i,
            campaignMapGalleryIndexId: vars.campaignMapGalleryIndexId,
            xCoord: sp.xCoord,
            yCoord: sp.yCoord,
            displayName: sp.displayName,
            locationScore: sp.locationScore,
          })),
        };
      });

      return { campaignMapGalleryIndexId: vars.campaignMapGalleryIndexId, key };
    },

    onSuccess: (data, _vars, ctx) => {
      // Replace the optimistic data with the authoritative server response so IDs
      // are correct for any future operations.
      queryClient.setQueryData<StartPositionsResponse>(ctx.key, data);

      const rollbackState = rollbackStateRef.current.get(ctx.campaignMapGalleryIndexId);
      if (rollbackState != null) {
        rollbackStateRef.current.set(ctx.campaignMapGalleryIndexId, {
          ...rollbackState,
          base: data,
        });
      }
    },

    onError: (err, _vars, ctx) => {
      // ctx is undefined if onMutate itself threw before returning its context; nothing to roll back in that case.
      if (ctx == null) {
        const msg = err instanceof Error ? err.message : "Failed to update start positions.";
        opts?.onFailureToast?.(msg);
        return;
      }

      const rollbackState = rollbackStateRef.current.get(ctx.campaignMapGalleryIndexId);
      if (rollbackState?.base != null) {
        queryClient.setQueryData(ctx.key, rollbackState.base);
      } else {
        queryClient.setQueryData<StartPositionsResponse>(ctx.key, {
          campaignMapGalleryIndexId: ctx.campaignMapGalleryIndexId,
          startPositions: [],
        });
      }

      const msg = err instanceof Error ? err.message : "Failed to update start positions.";
      opts?.onFailureToast?.(msg);
    },

    onSettled: (_data, _error, vars) => {
      const rollbackState = rollbackStateRef.current.get(vars.campaignMapGalleryIndexId);
      if (rollbackState == null) return;

      const pendingCount = rollbackState.pendingCount - 1;
      if (pendingCount <= 0) {
        rollbackStateRef.current.delete(vars.campaignMapGalleryIndexId);
        return;
      }

      rollbackStateRef.current.set(vars.campaignMapGalleryIndexId, {
        ...rollbackState,
        pendingCount,
      });
    },
  });
}
