import { renderHook } from "@testing-library/react";
import type { TileCell } from "@/features/map/components/TileTypes";
import type { Biome } from "@/types/map/Biome";
import type { Geography } from "@/types/map/Geography";
import { useTileRendering } from "../useTileRendering";

const GRASSLAND_COLOUR = "#4ade80";

/** One biome with a colour, so the base background under the fog is something recognisable. */
const BIOMES = [{ biomeType: 7, name: "Grassland", mapEditorHtmlColor: GRASSLAND_COLOUR } as Biome];

function tile(x: number, y: number, isExplored: boolean): TileCell {
  return { x, y, biomeTypeId: 7, geographyTypeId: 1, isExplored };
}

function renderTileRendering(observed: string[]) {
  return renderHook(() =>
    useTileRendering({
      biomes: BIOMES,
      geographies: [],
      selectedTile: null,
      observedTileKeys: new Set(observed),
    }),
  );
}

/** The background is built as a stack of gradient layers over the biome colour, so overlays are counted by looking for them. */
function overlayCount(background: string): number {
  return background.split("linear-gradient").length - 1;
}

/**
 * The map answers two questions per tile — has the player ever seen it, and is anything watching it now — and draws a different
 * overlay for each of the three answers that produces. Collapsing them into one flag is what turned the whole map grey behind a
 * force as soon as it moved.
 */
describe("useTileRendering fog layers", () => {
  it("draws no overlay at all on ground an observer is watching", () => {
    const { result } = renderTileRendering(["1,1"]);

    const background = result.current.getTileBackground(tile(1, 1, true));

    expect(overlayCount(background)).toBe(0);
    expect(background).toContain(GRASSLAND_COLOUR);
  });

  /** Explored but unwatched: the terrain under the haze is real and the player is meant to still be able to read it. */
  it("hazes explored ground that nothing is watching, over its real biome colour", () => {
    const { result } = renderTileRendering([]);

    const background = result.current.getTileBackground(tile(1, 1, true));

    expect(overlayCount(background)).toBe(1);
    expect(background).toContain(GRASSLAND_COLOUR);
  });

  it("fogs unexplored ground more heavily than explored ground that is merely unwatched", () => {
    const { result } = renderTileRendering([]);

    const unexplored = result.current.getTileBackground(tile(1, 1, false));
    const unwatched = result.current.getTileBackground(tile(2, 2, true));

    expect(unexplored).not.toEqual(unwatched);
    // Both draw exactly one overlay; they differ in which one, and the unexplored layer is the near-opaque of the two.
    expect(overlayCount(unexplored)).toBe(1);
    expect(unexplored).toContain("0.9");
  });

  /**
   * The two layers are mutually exclusive, and unexplored wins. A tile can be both unexplored and unwatched — most of the map
   * is — and stacking the haze on top of the fog would only darken ground that is already hidden.
   */
  it("draws only the unexplored fog on ground that is neither explored nor watched", () => {
    const { result } = renderTileRendering([]);

    const background = result.current.getTileBackground(tile(1, 1, false));

    expect(overlayCount(background)).toBe(1);
  });

  /**
   * The pinned regression: a step re-reports only what the moved force can see, so every other explored tile drops out of the
   * observed set. Those must fall back to haze, not to the fog that means "never been here".
   */
  it("keeps explored ground out of the unexplored fog when a step narrows what is observed", () => {
    const { result, rerender } = renderHook(({ observed }) =>
      useTileRendering({ biomes: BIOMES, geographies: [], selectedTile: null, observedTileKeys: new Set(observed) }),
    { initialProps: { observed: ["5,5"] } });

    const whileWatched = result.current.getTileBackground(tile(5, 5, true));

    // The force has stepped away and now reports a different set of tiles.
    rerender({ observed: ["6,5"] });

    const afterStep = result.current.getTileBackground(tile(5, 5, true));
    const neverSeen = result.current.getTileBackground(tile(5, 5, false));

    expect(overlayCount(whileWatched)).toBe(0);
    expect(overlayCount(afterStep)).toBe(1);
    expect(afterStep).not.toEqual(neverSeen);
  });
});

/**
 * The vision tool's selected tile is drawn twice over: a yellow layer in the background, and an outline the tile draws itself when
 * told it is selected. Both have to agree on which tile that is.
 */
describe("useTileRendering selection", () => {
  function renderWithSelection(selectedTile: { x: number; y: number } | null) {
    return renderHook(() => useTileRendering({ biomes: BIOMES, geographies: [], selectedTile, observedTileKeys: new Set(["1,1", "2,2"]) }));
  }

  it("reports only the selected tile as selected", () => {
    const { result } = renderWithSelection({ x: 1, y: 1 });

    expect(result.current.isTileSelected(tile(1, 1, true))).toBe(true);
    expect(result.current.isTileSelected(tile(2, 2, true))).toBe(false);
  });

  it("reports no tile as selected while nothing is", () => {
    const { result } = renderWithSelection(null);

    expect(result.current.isTileSelected(tile(1, 1, true))).toBe(false);
  });

  /** Watched ground has no fog layer, so the single overlay on the selected tile can only be the selection's. */
  it("layers the selection over the selected tile's background and nowhere else", () => {
    const { result } = renderWithSelection({ x: 1, y: 1 });

    expect(overlayCount(result.current.getTileBackground(tile(1, 1, true)))).toBe(1);
    expect(overlayCount(result.current.getTileBackground(tile(2, 2, true)))).toBe(0);
  });
});

describe("useTileRendering terrain label", () => {
  const GEOGRAPHIES = [{ campaignMapGeographyType: 1, name: "Hills" } as Geography];

  it("names the biome and geography together", () => {
    const { result } = renderHook(() =>
      useTileRendering({ biomes: BIOMES, geographies: GEOGRAPHIES, selectedTile: null, observedTileKeys: new Set() }),
    );

    expect(result.current.getTileTerrainLabel(tile(1, 1, true))).toBe("Grassland, Hills");
  });

  /** Unexplored ground carries the Unknown placeholder for both, and "Unknown, Unknown" says nothing the single word doesn't. */
  it("collapses to a single Unknown when both biome and geography are the Unknown placeholder", () => {
    const { result } = renderHook(() =>
      useTileRendering({
        biomes: [{ biomeType: 0, name: "Unknown" } as Biome],
        geographies: [{ campaignMapGeographyType: 0, name: "Unknown" } as Geography],
        selectedTile: null,
        observedTileKeys: new Set(),
      }),
    );

    expect(result.current.getTileTerrainLabel({ x: 1, y: 1, biomeTypeId: 0, geographyTypeId: 0, isExplored: false })).toBe("Unknown");
  });
});
