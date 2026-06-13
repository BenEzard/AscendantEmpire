import { useCallback, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import type { CampaignMapGalleryIndex } from "@/features/mapgallery/components/OpenCampaignMapDialog";
import { mapGalleryTilesKey } from "@/features/mapgallery/hooks/useCampaignMapTiles";

/** Progress info for a queued map generation, surfaced by the progress dialog. */
export interface MapGeneration {
  processingId: number;
  mapName: string;
}

/**
 * Owns which campaign map is currently open and the in-flight generation, plus the
 * query-cache lifecycle for the open map's tiles (open invalidates, close cancels +
 * removes so a late response can't repopulate a closed map).
 */
export function useMapEditorSession() {
  const queryClient = useQueryClient();
  const [openMapIndex, setOpenMapIndex] = useState<CampaignMapGalleryIndex | null>(null);
  const [generation, setGeneration] = useState<MapGeneration | null>(null);

  const openMap = useCallback(
    (index: CampaignMapGalleryIndex) => {
      // Ensure tiles reload fresh even if re-opening the same map.
      queryClient.invalidateQueries({ queryKey: mapGalleryTilesKey(index.campaignMapGalleryIndexId) });
      setOpenMapIndex(index);
    },
    [queryClient],
  );

  const closeMap = useCallback(() => {
    if (openMapIndex) {
      const key = mapGalleryTilesKey(openMapIndex.campaignMapGalleryIndexId);
      queryClient.cancelQueries({ queryKey: key });
      queryClient.removeQueries({ queryKey: key });
    }

    setOpenMapIndex(null);
  }, [openMapIndex, queryClient]);

  return { openMapIndex, setOpenMapIndex, generation, setGeneration, openMap, closeMap };
}
