import { useMutation, useQueryClient } from "@tanstack/react-query";
import { postUpdateCampaignMapTiles } from "../api/updateCampaignMapTilesApi";
import { mapGalleryTilesKey, type CampaignMapTilesResponse } from "./useCampaignMapTiles";
import type { BiomeType } from "@/types/map/Biome";
import type { CampaignMapGeographyType } from "@/types/map/Geography";

export type PaintCoord = { x: number; y: number };

export type UpdateCampaignMapTilesVariables = {
  campaignMapGalleryIndexId: number;
  coordinates: PaintCoord[];
  biomeType: BiomeType | null;
  geographyType: CampaignMapGeographyType | null;
};

type MutationContext = {
  previousTiles?: CampaignMapTilesResponse;
  key: ReturnType<typeof mapGalleryTilesKey>;
};

export function useUpdateCampaignMapTilesMutation(opts?: {
  onFailureToast?: (message: string) => void;
}) {
  const queryClient = useQueryClient();

  const formatFailures = (failures: { errorMessage: string; failedTiles: { coord: { x: number; y: number } }[] }[]) => {
    return failures
      .map((group) => {
        const coords = (group.failedTiles ?? [])
          .map((t) => `(${t.coord.x}, ${t.coord.y})`)
          .filter(Boolean)
          .join(", ");

        return coords ? `${group.errorMessage}: ${coords}` : group.errorMessage;
      })
      .filter(Boolean)
      .join("\n");
  };

  return useMutation({
    mutationFn: async (vars: UpdateCampaignMapTilesVariables) => {
      // Send both (x,y) and (xCoord,yCoord) so the backend can bind regardless of Coord shape.
      const coordinates = vars.coordinates.map((c) => ({ x: c.x, y: c.y, xCoord: c.x, yCoord: c.y }));
      return postUpdateCampaignMapTiles(vars.campaignMapGalleryIndexId, {
        coordinates,
        biomeType: vars.biomeType,
        geographyType: vars.geographyType,
      });
    },

    onMutate: async (vars): Promise<MutationContext> => {
      const key = mapGalleryTilesKey(vars.campaignMapGalleryIndexId);

      await queryClient.cancelQueries({ queryKey: key });

      const previousTiles = queryClient.getQueryData<CampaignMapTilesResponse>(key);

      queryClient.setQueryData<CampaignMapTilesResponse>(key, (old) => {
        if (!old) return old;

        const nextTiles = old.tiles.slice();

        for (const c of vars.coordinates) {
          const idx = nextTiles.findIndex((t) => t.xCoord === c.x && t.yCoord === c.y);
          if (idx < 0) continue;

          nextTiles[idx] = {
            ...nextTiles[idx],
            biomeTypeId: vars.biomeType,
            campaignMapGeographyTypeId: vars.geographyType,
          };
        }

        return { ...old, tiles: nextTiles };
      });

      return { previousTiles, key };
    },

    onSuccess: (data, _vars, ctx) => {
      // Revert only the tiles the server reports as failed.
      if (!data.failures?.length) return;

      queryClient.setQueryData<CampaignMapTilesResponse>(ctx.key, (old) => {
        if (!old) return old;

        const nextTiles = old.tiles.slice();

        for (const group of data.failures) {
          for (const ft of group.failedTiles ?? []) {
            const { x, y } = ft.coord;
            const idx = nextTiles.findIndex((t) => t.xCoord === x && t.yCoord === y);
            if (idx < 0) continue;

            nextTiles[idx] = {
              ...nextTiles[idx],
              biomeTypeId: ft.originalBiomeType,
              campaignMapGeographyTypeId: ft.originalGeographyType,
            };
          }
        }

        return { ...old, tiles: nextTiles };
      });

      const message = formatFailures(data.failures);

      if (message) opts?.onFailureToast?.(message);
    },

    onError: (err, _vars, ctx) => {
      if (ctx?.previousTiles) {
        queryClient.setQueryData(ctx.key, ctx.previousTiles);
      }

      const msg = err instanceof Error ? err.message : "Failed to paint tiles.";
      opts?.onFailureToast?.(msg);
    },
  });
}
