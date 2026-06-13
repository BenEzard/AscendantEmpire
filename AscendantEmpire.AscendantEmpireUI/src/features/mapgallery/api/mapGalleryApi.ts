import { http } from "../../../api/http";
import type { CampaignMapGalleryIndex } from "../components/OpenCampaignMapDialog"; // or move the type to /types

export type SearchMapGalleryResponse = { indexes: CampaignMapGalleryIndex[] };

export async function searchMapGalleryIndexes(signal?: AbortSignal): Promise<CampaignMapGalleryIndex[]> {
  const { data } = await http.get<SearchMapGalleryResponse>("/map-gallery/search", { signal });
  return data.indexes;
}

export async function updateMapProperties(
  id: number,
  mapName: string,
  description: string | null,
  signal?: AbortSignal,
): Promise<void> {
  await http.post(`/map-gallery/${id}/update`, { mapName, description }, { signal });
}
