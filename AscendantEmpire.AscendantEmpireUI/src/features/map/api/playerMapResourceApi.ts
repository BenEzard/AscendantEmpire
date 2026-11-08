import { http } from "@/api/http";

/**
 * One MapResource the player has discovered, on the tile it sits on.
 *
 * Carries the type's own display values rather than only its id, so a tile can be drawn without fetching the MapResource catalogue as well.
 */
export interface PlayerMapResource {
  x: number;
  y: number;

  /** Which of the tile's two places it takes: 1 or 2. Two resources on one tile are drawn in this order. */
  slot: number;

  mapResourceTypeId: number;
  code: string;
  displayName: string;

  /**
   * The image to draw on the tile, or null for a type the biome already says — a forest's wood. The game map draws no marker at all for those; see
   * MapResourceIcon, whose fish stand-in is for the map editor only.
   */
  iconPath: string | null;

  /**
   * Whether one of the player's observers can see the tile right now.
   *
   * The resource is reported either way, because the player has discovered it and what they found stays on their map. This says only whether a quantity
   * could have come with it, so a marker with no number is not mistaken for an empty patch.
   */
  isObserved: boolean;

  /**
   * What this player is shown in place of the true quantity, in whole units. An estimate, moved by their own fixed error — never the amount really on the
   * tile.
   *
   * Null where there is no number to give: the tile is not observed, or the resource holds its quantity in layers to survey or in per-asset amounts.
   */
  estimatedQuantity: number | null;
}

/** The response as it arrives. Every field is optional on the wire, so a missing one is read as absent rather than trusted. */
type PlayerMapResourceWireDto = {
  x?: number;
  y?: number;
  slot?: number;
  mapResourceTypeId?: number;
  code?: string;
  displayName?: string;
  iconPath?: string | null;
  isObserved?: boolean;
  estimatedQuantity?: number | null;
};

type PlayerMapResourcesWireDto = {
  resources?: PlayerMapResourceWireDto[] | null;
};

/**
 * Every MapResource the current player has discovered, across the whole map, ordered by tile and then slot.
 *
 * Read once for the map rather than per tile, so drawing a tile is a lookup. The game and player come from the game token, not the request.
 */
export async function getPlayerMapResources(): Promise<PlayerMapResource[]> {
  const { data } = await http.get<PlayerMapResourcesWireDto>("/campaign-map/player/map-resources");

  return (data.resources ?? []).map((resource) => ({
    x: resource.x ?? 0,
    y: resource.y ?? 0,
    slot: resource.slot ?? 0,
    mapResourceTypeId: resource.mapResourceTypeId ?? 0,
    code: resource.code ?? "",
    displayName: resource.displayName ?? "",
    iconPath: resource.iconPath ?? null,
    isObserved: resource.isObserved ?? false,
    estimatedQuantity: resource.estimatedQuantity ?? null,
  }));
}
