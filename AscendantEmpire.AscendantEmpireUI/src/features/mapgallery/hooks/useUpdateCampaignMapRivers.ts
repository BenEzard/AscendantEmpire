import { useMutation, useQueryClient } from "@tanstack/react-query";
import { putCampaignMapRivers, type RiverEdgeChangeDto } from "../api/updateCampaignMapRiversApi";
import { mapGalleryTilesKey, type CampaignMapTilesResponse } from "./useCampaignMapTiles";
import { apiErrorMessages } from "@/util/apiErrors";

export type UpdateCampaignMapRiversVariables = {
  campaignMapGalleryIndexId: number;
  changes: RiverEdgeChangeDto[];
};

type MutationContext = {
  previousTiles?: CampaignMapTilesResponse;
  key: ReturnType<typeof mapGalleryTilesKey>;
};

/**
 * Sets river edges on the open gallery map, patching the tile cache optimistically so the line appears under the pointer at once.
 *
 * The rollback is all-or-nothing, unlike the tile paint's: a river batch never partly lands, because the authoring rules judge the
 * network the whole batch would produce. So there is no per-edge reconciliation to do on success, and a refusal simply puts the
 * snapshot back.
 */
export function useUpdateCampaignMapRiversMutation(opts?: { onFailureToast?: (message: string) => void }) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (vars: UpdateCampaignMapRiversVariables) => {
      return putCampaignMapRivers(vars.campaignMapGalleryIndexId, { changes: vars.changes });
    },

    onMutate: async (vars): Promise<MutationContext> => {
      const key = mapGalleryTilesKey(vars.campaignMapGalleryIndexId);

      await queryClient.cancelQueries({ queryKey: key });

      const previousTiles = queryClient.getQueryData<CampaignMapTilesResponse>(key);

      queryClient.setQueryData<CampaignMapTilesResponse>(key, (old) => {
        if (!old) return old;

        const nextTiles = old.tiles.slice();

        for (const change of vars.changes) {
          const idx = nextTiles.findIndex((t) => t.xCoord === change.x && t.yCoord === change.y);
          if (idx < 0) continue;

          nextTiles[idx] =
            change.edge === "South" ? { ...nextTiles[idx], southRiver: change.width } : { ...nextTiles[idx], eastRiver: change.width };
        }

        return { ...old, tiles: nextTiles };
      });

      return { previousTiles, key };
    },

    onError: (error, _vars, ctx) => {
      if (ctx?.previousTiles) {
        queryClient.setQueryData(ctx.key, ctx.previousTiles);
      }

      // The server's own wording, which names the rule that was broken and often an edge the author did not click.
      const messages = apiErrorMessages(error);

      opts?.onFailureToast?.(messages.length > 0 ? messages.join(" ") : "The river edge could not be saved.");
    },
  });
}
