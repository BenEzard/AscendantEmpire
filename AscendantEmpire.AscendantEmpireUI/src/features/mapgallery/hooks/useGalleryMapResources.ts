import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  deleteGalleryMapResource,
  getGalleryMapResources,
  placeGalleryMapResource,
  updateGalleryMapResource,
  type GalleryMapResource,
  type GalleryMapResourceSpecifics,
} from "../api/galleryMapResourcesApi";

export const galleryMapResourcesKey = (campaignMapGalleryIndexId: number | null) => ["map-gallery", campaignMapGalleryIndexId, "map-resources"] as const;

/** The MapResources placed on the open map. Disabled while no map is open. */
export function useGalleryMapResourcesQuery(campaignMapGalleryIndexId: number | null) {
  return useQuery({
    queryKey: galleryMapResourcesKey(campaignMapGalleryIndexId),
    queryFn: () => getGalleryMapResources(campaignMapGalleryIndexId!),
    enabled: campaignMapGalleryIndexId != null,
  });
}

/**
 * Placing, updating and removing a placement on the open map. Each refetches the map's placements on success, so the tiles redraw from the server's
 * view (the slot a placement took is the server's decision).
 */
export function useGalleryMapResourceMutations(campaignMapGalleryIndexId: number | null) {
  const queryClient = useQueryClient();
  const refresh = () => queryClient.invalidateQueries({ queryKey: galleryMapResourcesKey(campaignMapGalleryIndexId) });

  const place = useMutation({
    mutationFn: (placement: { xCoord: number; yCoord: number; mapResourceTypeId: number }) => placeGalleryMapResource(campaignMapGalleryIndexId!, placement),
    onSuccess: refresh,
  });

  const update = useMutation({
    mutationFn: ({ placementId, specifics }: { placementId: number; specifics: GalleryMapResourceSpecifics }) =>
      updateGalleryMapResource(campaignMapGalleryIndexId!, placementId, specifics),
    onSuccess: refresh,
  });

  const remove = useMutation({
    mutationFn: (placement: GalleryMapResource) => deleteGalleryMapResource(campaignMapGalleryIndexId!, placement.id),
    onSuccess: refresh,
  });

  return { place, update, remove };
}
