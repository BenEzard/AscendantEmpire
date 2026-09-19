import { http } from "../../../api/http";

/** A MapResource placed on a gallery map tile (GET/POST/PUT /map-gallery/{id}/map-resources). */
export type GalleryMapResource = {
  id: number;
  xCoord: number;
  yCoord: number;
  slot: number; // 1 or 2: a tile holds at most two MapResources.
  mapResourceTypeId: number;
  initialQuantity: number | null; // null = rolled from the habitat's range when a game is created.
  distributionScenarioNumber: number | null; // null = picked, weighted by Biome, when a game is created.
};

/** The specifics the placement dialog sets. Each left null is decided when a game is created. */
export type GalleryMapResourceSpecifics = {
  initialQuantity: number | null;
  distributionScenarioNumber: number | null;
};

export async function getGalleryMapResources(campaignMapGalleryIndexId: number): Promise<GalleryMapResource[]> {
  const { data } = await http.get<{ mapResources: GalleryMapResource[] }>(`/map-gallery/${campaignMapGalleryIndexId}/map-resources`);
  return data.mapResources;
}

/** Places a MapResource in the tile's first free slot. */
export async function placeGalleryMapResource(
  campaignMapGalleryIndexId: number,
  placement: { xCoord: number; yCoord: number; mapResourceTypeId: number },
): Promise<GalleryMapResource> {
  const { data } = await http.post<GalleryMapResource>(`/map-gallery/${campaignMapGalleryIndexId}/map-resources`, placement);
  return data;
}

export async function updateGalleryMapResource(
  campaignMapGalleryIndexId: number,
  placementId: number,
  specifics: GalleryMapResourceSpecifics,
): Promise<GalleryMapResource> {
  const { data } = await http.put<GalleryMapResource>(`/map-gallery/${campaignMapGalleryIndexId}/map-resources/${placementId}`, specifics);
  return data;
}

export async function deleteGalleryMapResource(campaignMapGalleryIndexId: number, placementId: number): Promise<void> {
  await http.delete(`/map-gallery/${campaignMapGalleryIndexId}/map-resources/${placementId}`);
}
