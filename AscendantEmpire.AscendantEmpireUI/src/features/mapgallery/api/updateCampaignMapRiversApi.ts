import { http } from "@/api/http";
import type { RiverWidth } from "@/types/map/Rivers";
import type { OwnedRiverEdgeSide } from "../tools/riverEdgePicking";

/**
 * One river edge to set, named the way the server stores it: the tile that owns the edge, and which of that tile's two edges it is.
 *
 * Only South and East exist on the wire, so a click on a tile's north or west edge is translated to the owning neighbour before it
 * gets here — see `toCanonicalEdge`.
 */
export type RiverEdgeChangeDto = {
  x: number;
  y: number;
  edge: OwnedRiverEdgeSide;
  /** `"None"` erases the river from the edge. */
  width: RiverWidth;
};

export type UpdateCampaignMapRiversDto = {
  changes: RiverEdgeChangeDto[];
};

export type UpdateCampaignMapRiversResponse = {
  edgesUpdated: number;
};

/**
 * An edge the server refused, and the width it still holds.
 *
 * Not always an edge the request named: widening one can be refused because of a river further inland, and erasing one because of
 * the stretch it would cut off.
 */
export type RiverEdgeUpdateFail = {
  x: number;
  y: number;
  edge: OwnedRiverEdgeSide;
  originalWidth: RiverWidth;
  errorMessage: string;
};

/** The error detail a refusal carries, alongside the human-readable messages `apiErrorMessages` picks up. */
export type RiverEdgeUpdateFailCollection = {
  refusedEdges: RiverEdgeUpdateFail[];
};

/**
 * Sets river edges on a gallery map.
 *
 * The batch lands whole or not at all: the authoring rules judge the river network the changes would produce, so a refusal answers
 * 400 and nothing is written. The caller therefore rolls its optimistic copy back in full rather than edge by edge.
 */
export async function putCampaignMapRivers(
  campaignMapGalleryIndexId: number,
  dto: UpdateCampaignMapRiversDto,
  signal?: AbortSignal,
): Promise<UpdateCampaignMapRiversResponse> {
  const { data } = await http.put<UpdateCampaignMapRiversResponse>(`/map-gallery/${campaignMapGalleryIndexId}/rivers`, dto, {
    signal,
    headers: { "Content-Type": "application/json" },
  });

  return data;
}
