import { renderHook } from "@testing-library/react";
import type { TileCell } from "@/features/map/components/TileTypes";
import type { Biome } from "@/types/map/Biome";
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
function overlayCount(background: string | undefined): number {
  return (background ?? "").split("linear-gradient").length - 1;
}

/**
 * The map answers two questions per tile — has the player ever seen it, and is anything watching it now — and draws a different
 * overlay for each of the three answers that produces. Collapsing them into one flag is what turned the whole map grey behind a
 * force as soon as it moved.
 */
describe("useTileRendering fog layers", () => {
  it("draws no overlay at all on ground an observer is watching", () => {
    const { result } = renderTileRendering(["1,1"]);

    const style = result.current.getTileStyle(tile(1, 1, true));

    expect(overlayCount(style.background as string)).toBe(0);
    expect(style.background).toContain(GRASSLAND_COLOUR);
  });

  /** Explored but unwatched: the terrain under the haze is real and the player is meant to still be able to read it. */
  it("hazes explored ground that nothing is watching, over its real biome colour", () => {
    const { result } = renderTileRendering([]);

    const style = result.current.getTileStyle(tile(1, 1, true));

    expect(overlayCount(style.background as string)).toBe(1);
    expect(style.background).toContain(GRASSLAND_COLOUR);
  });

  it("fogs unexplored ground more heavily than explored ground that is merely unwatched", () => {
    const { result } = renderTileRendering([]);

    const unexplored = result.current.getTileStyle(tile(1, 1, false)).background as string;
    const unwatched = result.current.getTileStyle(tile(2, 2, true)).background as string;

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

    const style = result.current.getTileStyle(tile(1, 1, false));

    expect(overlayCount(style.background as string)).toBe(1);
  });

  /**
   * The pinned regression: a step re-reports only what the moved force can see, so every other explored tile drops out of the
   * observed set. Those must fall back to haze, not to the fog that means "never been here".
   */
  it("keeps explored ground out of the unexplored fog when a step narrows what is observed", () => {
    const { result, rerender } = renderHook(({ observed }) =>
      useTileRendering({ biomes: BIOMES, geographies: [], selectedTile: null, observedTileKeys: new Set(observed) }),
    { initialProps: { observed: ["5,5"] } });

    const whileWatched = result.current.getTileStyle(tile(5, 5, true)).background as string;

    // The force has stepped away and now reports a different set of tiles.
    rerender({ observed: ["6,5"] });

    const afterStep = result.current.getTileStyle(tile(5, 5, true)).background as string;
    const neverSeen = result.current.getTileStyle(tile(5, 5, false)).background as string;

    expect(overlayCount(whileWatched)).toBe(0);
    expect(overlayCount(afterStep)).toBe(1);
    expect(afterStep).not.toEqual(neverSeen);
  });
});
