import { http } from "../../../api/http";

export interface CampaignMapGalleryIndex {
  campaignMapGalleryIndexId: number;
  mapName: string;
  description: string;
  width: number;
  height: number;
}

export type CampaignMapPreviewMode = "biome" | "geography" | "startpositions";

export type SearchMapGalleryResponse = { indexes: CampaignMapGalleryIndex[] };

export const mapGalleryIndexesKey = ["mapGalleryIndexes"] as const;

export async function searchMapGalleryIndexes(signal?: AbortSignal): Promise<CampaignMapGalleryIndex[]> {
  const { data } = await http.get<SearchMapGalleryResponse>("/map-gallery/search", { signal });
  return data.indexes;
}

/**
 * Fetches a map preview image as a blob.
 *
 * A plain `<img src>` cannot send an Authorization header, and this endpoint requires a token like every
 * other one, so the image is fetched through the shared client and handed to the caller as a blob to turn
 * into an object URL — the same approach the catalogue and map exports already use for their downloads.
 *
 * The caller owns the object URL it creates and must revoke it.
 */
export async function fetchCampaignMapPreview(
  campaignMapGalleryIndexId: number,
  mode: CampaignMapPreviewMode,
  signal?: AbortSignal,
): Promise<Blob> {
  const { data } = await http.get<Blob>(`/map-gallery/${campaignMapGalleryIndexId}/preview`, {
    params: { mode },
    responseType: "blob",
    signal,
  });

  return data;
}

export async function updateMapProperties(
  id: number,
  mapName: string,
  description: string | null,
  signal?: AbortSignal,
): Promise<void> {
  await http.post(`/map-gallery/${id}/update`, { mapName, description }, { signal });
}
