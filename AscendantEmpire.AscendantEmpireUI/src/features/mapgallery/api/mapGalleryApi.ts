import { http } from "../../../api/http";

export interface CampaignMapGalleryIndex {
  campaignMapGalleryIndexId: number;
  mapName: string;
  description: string;
  width: number;
  height: number;
}

export type CampaignMapPreviewMode = "biome" | "geography";

export type SearchMapGalleryResponse = { indexes: CampaignMapGalleryIndex[] };

export const mapGalleryIndexesKey = ["mapGalleryIndexes"] as const;

export async function searchMapGalleryIndexes(signal?: AbortSignal): Promise<CampaignMapGalleryIndex[]> {
  const { data } = await http.get<SearchMapGalleryResponse>("/map-gallery/search", { signal });
  return data.indexes;
}

export function getCampaignMapPreviewUrl(campaignMapGalleryIndexId: number, mode: CampaignMapPreviewMode): string {
  const path = `/map-gallery/${campaignMapGalleryIndexId}/preview?mode=${mode}`;
  return new URL(path, http.defaults.baseURL).toString();
}

export async function updateMapProperties(
  id: number,
  mapName: string,
  description: string | null,
  signal?: AbortSignal,
): Promise<void> {
  await http.post(`/map-gallery/${id}/update`, { mapName, description }, { signal });
}
