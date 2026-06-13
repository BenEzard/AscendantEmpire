import { useQuery } from "@tanstack/react-query";
import axios from "axios";

export interface CampaignMapTileDto {
  campaignMapGalleryIndexId: number;
  xCoord: number;
  yCoord: number;
  biomeTypeId: number | null;
  campaignMapGeographyTypeId: number | null;
}

export interface CampaignMapTilesResponse {
  campaignMapGalleryIndexId: number;
  tiles: CampaignMapTileDto[];
}

export const mapGalleryTilesKey = (campaignMapGalleryIndexId: number | null) => ["mapGalleryTiles", campaignMapGalleryIndexId] as const;

const API_BASE = "https://localhost:7242";

export function useCampaignMapTilesQuery(campaignMapGalleryIndexId: number | null, enabled: boolean) {
  return useQuery<CampaignMapTilesResponse, Error>({
    queryKey: mapGalleryTilesKey(campaignMapGalleryIndexId),
    enabled: enabled && campaignMapGalleryIndexId != null,
    refetchOnWindowFocus: false,
    queryFn: async ({ signal }) => {
      if (campaignMapGalleryIndexId == null) throw new Error("campaignMapGalleryIndexId is null");
      // Pass react-query's AbortSignal so switching maps cancels the in-flight request
      // and a late response can't overwrite the newer map's tiles in the cache.
      const res = await axios.get<CampaignMapTilesResponse>(`${API_BASE}/map-gallery/${campaignMapGalleryIndexId}/tiles`, { signal });
      return res.data;
    },
  });
}
