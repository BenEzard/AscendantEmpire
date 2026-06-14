import { http } from "../../../api/http";
import type { CampaignMapGalleryIndex } from "@/features/mapgallery/api/mapGalleryApi";

export type SearchMapGalleryResponse = { indexes: CampaignMapGalleryIndex[] };

export async function searchMapGalleryIndexes(signal?: AbortSignal): Promise<CampaignMapGalleryIndex[]> {
  const { data } = await http.get<SearchMapGalleryResponse>("/map-gallery/search", { signal });
  return data.indexes;
}
