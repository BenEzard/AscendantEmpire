import fireImage from "@/assets/structures/fire_40px.png";
import grassHutImage from "@/assets/structures/grass-hut_40px.png";
import pallisadeImage from "@/assets/structures/pallisade_40px.png";
import woodTotemPoleImage from "@/assets/structures/wood-totem-pole_40px.png";
import woodenWatchTowerImage from "@/assets/structures/wooden-watch-tower_40px.png";

/**
 * The map art the client has for each kind of structure, by the kind's catalogue code.
 *
 * Keyed on the code rather than on `structureId`, which is what this used to be. A catalogue id is a database identity and so
 * differs between databases — the ids only ever lined up because the single row anyone had was inserted by hand. The codes come
 * from StructureSeedData and are stable, so a rename there is the one thing that breaks a mapping here.
 *
 * Shared by the live structure on a tile and the last-known-structure marker, so the two always draw a kind the same way.
 */
const STRUCTURE_IMAGE_SRC_BY_CODE: ReadonlyMap<string, string> = new Map([
  ["camp", fireImage],
  ["village", grassHutImage],
  ["wooden-watch-tower", woodenWatchTowerImage],
  ["palisade", pallisadeImage],
  ["wooden-totem-pole", woodTotemPoleImage],
]);

/** The map image for a kind of structure, or undefined when the kind is not known or the client has no art for it. */
export function structureImageSrc(structureCode: string | null | undefined): string | undefined {
  if (!structureCode) {
    return undefined;
  }

  return STRUCTURE_IMAGE_SRC_BY_CODE.get(structureCode);
}
