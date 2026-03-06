import { http } from "@/api/http";

export async function postPublishCampaignMap(campaignMapGalleryIndexId: number, signal?: AbortSignal): Promise<void> {
  await http.post(`/map-gallery/${campaignMapGalleryIndexId}/publish`, undefined, { signal });
}