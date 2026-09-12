import woodenWatchTowerImage from "@/assets/structures/wooden-watch-tower_40px.png";

/**
 * The map art the client has for each kind of structure, by structure id.
 *
 * Only the wooden watch tower has art so far. Shared by the live structure on a tile and the last-known structure marker, so the two always draw a kind the same
 * way.
 */
const STRUCTURE_IMAGE_SRC_BY_STRUCTURE_ID: ReadonlyMap<number, string> = new Map([[1, woodenWatchTowerImage]]);

/** The map image for a kind of structure, or undefined when the kind is not known or the client has no art for it. */
export function structureImageSrc(structureId: number | null | undefined): string | undefined {
  if (structureId == null) {
    return undefined;
  }

  return STRUCTURE_IMAGE_SRC_BY_STRUCTURE_ID.get(structureId);
}
