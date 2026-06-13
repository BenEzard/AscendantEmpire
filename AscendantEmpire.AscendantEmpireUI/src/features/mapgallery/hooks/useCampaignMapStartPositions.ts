import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
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
  previous?: StartPositionsResponse;
  key: ReturnType<typeof mapGalleryStartPositionsKey>;
};

export function usePutCampaignMapStartPositionsMutation(opts?: {
  onFailureToast?: (message: string) => void;
}) {
  const queryClient = useQueryClient();

  return useMutation<StartPositionsResponse, Error, PutStartPositionsVariables, MutationContext>({
    mutationFn: async (vars) =>
      putCampaignMapStartPositions(vars.campaignMapGalleryIndexId, {
        startPositions: vars.startPositions,
      }),

    onMutate: async (vars): Promise<MutationContext> => {
      const key = mapGalleryStartPositionsKey(vars.campaignMapGalleryIndexId);

      await queryClient.cancelQueries({ queryKey: key });

      const previous = queryClient.getQueryData<StartPositionsResponse>(key);

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

      return { previous, key };
    },

    onSuccess: (data, _vars, ctx) => {
      // Replace the optimistic data with the authoritative server response so IDs
      // are correct for any future operations.
      queryClient.setQueryData<StartPositionsResponse>(ctx.key, data);
    },

    onError: (err, _vars, ctx) => {
      if (ctx?.previous != null) {
        queryClient.setQueryData(ctx.key, ctx.previous);
      }

      const msg = err instanceof Error ? err.message : "Failed to update start positions.";
      opts?.onFailureToast?.(msg);
    },
  });
}
