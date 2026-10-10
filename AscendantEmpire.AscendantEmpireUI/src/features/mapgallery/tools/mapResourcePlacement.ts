/**
 * Pure helpers for placing MapResources on a gallery map in the editor: grouping placements by tile, finding the habitat that applies to a tile, and
 * deciding what a click with the MapResource tool should do.
 */
import type { MapResourceCategory, MapResourceHabitat, MapResourceType } from "@/types/map/MapResource";
import type { GalleryMapResource } from "../api/galleryMapResourcesApi";
import type { BiomeType } from "@/types/map/Biome";
import type { CampaignMapGeographyType } from "@/types/map/Geography";

/** The "x,y" key tiles are indexed by, as start positions are. */
export function tileKey(x: number, y: number): string {
  return `${x},${y}`;
}

/** Every placement on the map, grouped by tile and ordered by slot. */
export function placementsByTile(placements: GalleryMapResource[] | undefined): ReadonlyMap<string, GalleryMapResource[]> {
  const byTile = new Map<string, GalleryMapResource[]>();

  for (const placement of placements ?? []) {
    const key = tileKey(placement.xCoord, placement.yCoord);
    byTile.set(key, [...(byTile.get(key) ?? []), placement]);
  }

  for (const list of byTile.values()) {
    list.sort((a, b) => a.slot - b.slot);
  }

  return byTile;
}

/** Every type in the catalogue, by id. */
export function typesById(categories: MapResourceCategory[] | undefined): ReadonlyMap<number, MapResourceType> {
  return new Map((categories ?? []).flatMap((category) => category.types).filter((type) => type.id != null).map((type) => [type.id!, type]));
}

/**
 * The habitat that sets the type's values on a tile: one naming the tile's Geography wins over the Biome-wide one. Undefined when neither exists, which
 * means the type cannot exist on the tile at all.
 */
export function habitatFor(type: MapResourceType, biomeType: BiomeType | null, geographyType: CampaignMapGeographyType | null): MapResourceHabitat | undefined {
  const inBiome = type.habitats.filter((habitat) => habitat.biomeType === biomeType);

  return inBiome.find((habitat) => habitat.geographyType != null && habitat.geographyType === geographyType) ?? inBiome.find((habitat) => habitat.geographyType == null);
}

/** What a click on a tile with the MapResource tool does. */
export type PlacementClick =
  | { action: "none"; reason: string }
  | { action: "edit"; placement: GalleryMapResource }
  | { action: "place" };

/**
 * A click places the selected type, unless the tile already holds it, in which case its dialog opens instead. The server is the one that refuses an
 * incompatible or full tile; this only saves a round trip for the case it can answer from what is already loaded.
 */
export function resolvePlacementClick(selectedTypeId: number | null, onTile: GalleryMapResource[] | undefined): PlacementClick {
  if (selectedTypeId == null) {
    return { action: "none", reason: "Select a MapResource to place from the MapResources palette." };
  }

  const existing = onTile?.find((placement) => placement.mapResourceTypeId === selectedTypeId);

  return existing ? { action: "edit", placement: existing } : { action: "place" };
}
