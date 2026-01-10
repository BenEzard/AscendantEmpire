import { useQuery } from "@tanstack/react-query";
import axios from "axios";

export interface CampaignMapTileDto {
  campaignMapGalleryIndexId: number;
  xCoord: number;
  yCoord: number;
  biomeTypeId: number;
  campaignMapGeographyTypeId: number;
}

export interface CampaignMapTilesResponse {
  campaignMapGalleryIndexId: number;
  tiles: CampaignMapTileDto[];
}

const API_BASE = "https://localhost:7242";

export function useCampaignMapTilesQuery(campaignMapGalleryIndexId: number | null, enabled: boolean) {
  return useQuery<CampaignMapTilesResponse, Error>({
    queryKey: ["mapGalleryTiles", campaignMapGalleryIndexId],
    enabled: enabled && campaignMapGalleryIndexId != null,
    refetchOnWindowFocus: false,
    queryFn: async () => {
      if (campaignMapGalleryIndexId == null) throw new Error("campaignMapGalleryIndexId is null");
      const res = await axios.get<CampaignMapTilesResponse>(`${API_BASE}/map-gallery/${campaignMapGalleryIndexId}/tiles`);
      return res.data;
    },
  });
}
