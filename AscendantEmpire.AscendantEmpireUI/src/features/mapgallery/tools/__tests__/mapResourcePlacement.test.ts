import { describe, expect, it } from "vitest";
import type { MapResourceType } from "@/types/map/MapResource";
import type { GalleryMapResource } from "../../api/galleryMapResourcesApi";
import { createHabitat, createMapResourceType } from "../mapResourceDraft";
import { habitatFor, placementsByTile, resolvePlacementClick, tileKey, typesById } from "../mapResourcePlacement";

// Biome and Geography arrive as their enum member names rather than numbers — see BiomeType in @/types/map/Biome.
const FOREST = "Forest";
const WOODLAND = "Woodland";
const FLAT = "Flat";
const HILLY = "Hilly";

function placement(id: number, x: number, y: number, slot: number, mapResourceTypeId: number): GalleryMapResource {
  return { id, xCoord: x, yCoord: y, slot, mapResourceTypeId, initialQuantity: null, distributionScenarioNumber: null };
}

/** A renewable type with a Biome-wide Forest habitat (ceiling 500) and a Hilly Forest one (ceiling 900). */
function berry(): MapResourceType {
  return {
    ...createMapResourceType("food", 10),
    id: 1,
    habitats: [
      { ...createHabitat("renewable", FOREST), maximumQuantity: 500 },
      { ...createHabitat("renewable", FOREST), geographyType: HILLY, maximumQuantity: 900 },
    ],
  };
}

describe("placementsByTile", () => {
  it("groups placements by tile, in slot order", () => {
    const byTile = placementsByTile([placement(1, 0, 0, 2, 5), placement(2, 1, 0, 1, 5), placement(3, 0, 0, 1, 6)]);

    expect(byTile.get(tileKey(0, 0))?.map((p) => p.id)).toEqual([3, 1]);
    expect(byTile.get(tileKey(1, 0))?.map((p) => p.id)).toEqual([2]);
    expect(byTile.get(tileKey(5, 5))).toBeUndefined();
  });
});

describe("typesById", () => {
  it("indexes every saved type across categories", () => {
    const byId = typesById([{ code: "food", displayName: "Food", sortOrder: 10, types: [berry()] }]);

    expect(byId.get(1)?.displayName).toBe("New MapResource");
  });
});

describe("habitatFor", () => {
  it("prefers the habitat naming the tile's Geography", () => {
    expect(habitatFor(berry(), FOREST, HILLY)?.maximumQuantity).toBe(900);
  });

  it("falls back to the Biome-wide habitat", () => {
    expect(habitatFor(berry(), FOREST, FLAT)?.maximumQuantity).toBe(500);
  });

  it("finds nothing for a Biome with no habitat", () => {
    expect(habitatFor(berry(), WOODLAND, FLAT)).toBeUndefined();
  });
});

describe("resolvePlacementClick", () => {
  it("asks for a selection when nothing is selected", () => {
    expect(resolvePlacementClick(null, []).action).toBe("none");
  });

  it("opens the existing placement when the tile already holds the selected type", () => {
    const existing = placement(4, 0, 0, 1, 7);

    expect(resolvePlacementClick(7, [existing])).toEqual({ action: "edit", placement: existing });
  });

  it("places the selected type otherwise, leaving a full or incompatible tile for the server to refuse", () => {
    expect(resolvePlacementClick(7, [placement(4, 0, 0, 1, 8), placement(5, 0, 0, 2, 9)]).action).toBe("place");
    expect(resolvePlacementClick(7, undefined).action).toBe("place");
  });
});
