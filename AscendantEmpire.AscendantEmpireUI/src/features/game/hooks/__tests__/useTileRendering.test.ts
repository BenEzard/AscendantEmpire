import { renderHook } from "@testing-library/react";
import type { TileCell } from "@/features/map/components/TileTypes";
import type { Biome } from "@/types/map/Biome";
import type { Geography } from "@/types/map/Geography";
import type { TileOwnership } from "@/types/map/Ownership";
import { useTileRendering } from "../useTileRendering";

const GRASSLAND_COLOUR = "#4ade80";

/** One biome with a colour, so the base background under the fog is something recognisable. */
const BIOMES = [{ biomeType: "Grassland", name: "Grassland", mapEditorHtmlColor: GRASSLAND_COLOUR } as Biome];

/** Two factions, so a tooltip naming whoever holds a tile has somebody to name and a rival to tell them apart from. */
const FACTION_NAMES = new Map([
  [1, "Rome"],
  [2, "Carthage"],
  [3, "Numidia"],
]);

function tile(x: number, y: number, isExplored: boolean): TileCell {
  // Biome and Geography arrive as their enum member names, not numbers — see BiomeType in @/types/map/Biome.
  return { x, y, biomeTypeId: "Grassland", geographyTypeId: "Hilly", isExplored };
}

function renderTileRendering(observed: string[]) {
  return renderHook(() =>
    useTileRendering({
      biomes: BIOMES,
      geographies: [],
      selectedTile: null,
      observedTileKeys: new Set(observed),
      factionNameByPlayerId: FACTION_NAMES,
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
      useTileRendering({
        biomes: BIOMES,
        geographies: [],
        selectedTile: null,
        observedTileKeys: new Set(observed),
        factionNameByPlayerId: FACTION_NAMES,
      }),
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
    return renderHook(() =>
      useTileRendering({
        biomes: BIOMES,
        geographies: [],
        selectedTile,
        observedTileKeys: new Set(["1,1", "2,2"]),
        factionNameByPlayerId: FACTION_NAMES,
      }),
    );
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
  const GEOGRAPHIES = [{ campaignMapGeographyType: "Hilly", name: "Hills" } as Geography];

  it("names the biome and geography together", () => {
    const { result } = renderHook(() =>
      useTileRendering({
        biomes: BIOMES,
        geographies: GEOGRAPHIES,
        selectedTile: null,
        observedTileKeys: new Set(),
        factionNameByPlayerId: FACTION_NAMES,
      }),
    );

    expect(result.current.getTileTerrainLabel(tile(1, 1, true))).toBe("Grassland, Hills");
  });

  /** Unexplored ground carries the Unknown placeholder for both, and "Unknown, Unknown" says nothing the single word doesn't. */
  it("collapses to a single Unknown when both biome and geography are the Unknown placeholder", () => {
    const { result } = renderHook(() =>
      useTileRendering({
        biomes: [{ biomeType: "Unknown", name: "Unknown" } as Biome],
        geographies: [{ campaignMapGeographyType: "Unknown", name: "Unknown" } as Geography],
        selectedTile: null,
        observedTileKeys: new Set(),
        factionNameByPlayerId: FACTION_NAMES,
      }),
    );

    expect(result.current.getTileTerrainLabel({ x: 1, y: 1, biomeTypeId: "Unknown", geographyTypeId: "Unknown", isExplored: false })).toBe("Unknown");
  });
});

/**
 * A river runs along a tile's edge rather than on the tile, so nothing about the tile's colour or icon says it is there. The hover
 * tooltip is where the player reads which of its sides one borders.
 */
describe("useTileRendering river tooltip", () => {
  function renderWithTile(t: TileCell) {
    const { result } = renderHook(() =>
      useTileRendering({
        biomes: BIOMES,
        geographies: [],
        selectedTile: null,
        observedTileKeys: new Set(),
        factionNameByPlayerId: FACTION_NAMES,
      }),
    );

    return result.current.getTileTitle(t);
  }

  it("lists the tile's river edges by side, clockwise from the north", () => {
    const title = renderWithTile({ ...tile(3, 4, true), rivers: { north: "Medium", east: "None", south: "None", west: "Large" } });

    expect(title).toContain("Rivers: N medium, W large");
  });

  it("leaves the river line out entirely for a tile with no rivers on it", () => {
    const title = renderWithTile({ ...tile(3, 4, true), rivers: { north: "None", east: "None", south: "None", west: "None" } });

    expect(title).not.toContain("Rivers");
  });

  /** Unexplored ground reports no rivers at all, and "no rivers here" is a claim about ground the player has never seen. */
  it("leaves the river line out for unexplored ground", () => {
    const title = renderWithTile(tile(3, 4, false));

    expect(title).not.toContain("Rivers");
  });
});

/**
 * Ownership moves, unlike a tile's terrain, so the API reports it only under live sight and a tile the player merely remembers
 * carries none. The tooltip is where they read whose ground they are looking at.
 */
describe("useTileRendering ownership tooltip", () => {
  function renderWithTile(t: TileCell) {
    const { result } = renderHook(() =>
      useTileRendering({
        biomes: BIOMES,
        geographies: [],
        selectedTile: null,
        observedTileKeys: new Set(),
        factionNameByPlayerId: FACTION_NAMES,
      }),
    );

    return result.current.getTileTitle(t);
  }

  /** The ownership record a watched tile carries, with the layers the caller cares about overridden. */
  function ownership(overrides: Partial<TileOwnership>): TileOwnership {
    return {
      status: "Unclaimed",
      ownedByPlayerId: null,
      occupiedByPlayerId: null,
      lastOwnedByPlayerId: null,
      effectiveHolderPlayerId: null,
      claimedByPlayerIds: [],
      isRebelling: false,
      ...overrides,
    };
  }

  it("names the faction holding an owned tile", () => {
    const title = renderWithTile({
      ...tile(3, 4, true),
      ownership: ownership({ status: "Owned", ownedByPlayerId: 1, effectiveHolderPlayerId: 1 }),
    });

    expect(title).toContain("Owned by Rome");
  });

  /**
   * An occupier collects the yield without taking ownership, so the holder the tooltip names is the occupier while the status
   * beside it still says what has happened to the tile.
   */
  it("names the occupier rather than the owner on an occupied tile", () => {
    const title = renderWithTile({
      ...tile(3, 4, true),
      ownership: ownership({ status: "Occupied", ownedByPlayerId: 1, occupiedByPlayerId: 2, effectiveHolderPlayerId: 2 }),
    });

    expect(title).toContain("Occupied by Carthage");
    expect(title).not.toContain("Rome");
  });

  /** Rebelling is a modifier on Owned, not a state of its own, so it reads alongside the status rather than replacing it. */
  it("marks a rebelling tile alongside its owner rather than instead of them", () => {
    const title = renderWithTile({
      ...tile(3, 4, true),
      ownership: ownership({ status: "Owned", ownedByPlayerId: 1, effectiveHolderPlayerId: 1, isRebelling: true }),
    });

    expect(title).toContain("Owned by Rome (rebelling)");
  });

  /** An abandoned tile is held by nobody, but it remembers whose it was until it reverts. */
  it("names the former owner of an abandoned tile, which nobody holds", () => {
    const title = renderWithTile({
      ...tile(3, 4, true),
      ownership: ownership({ status: "Abandoned", lastOwnedByPlayerId: 1 }),
    });

    expect(title).toContain("Abandoned by Rome");
  });

  /** Wilderness is most of the map, and a line reading "Unclaimed" on every tile of it says nothing. */
  it("leaves the ownership line out for a watched tile nobody holds", () => {
    const title = renderWithTile({ ...tile(3, 4, true), ownership: ownership({ status: "Unclaimed" }) });

    expect(title).not.toContain("Unclaimed");
  });

  /** The pinned rule: off live sight the API sends no ownership at all, so there is nothing to describe rather than a stale border. */
  it("leaves the ownership line out for ground nothing is watching", () => {
    const title = renderWithTile({ ...tile(3, 4, true), ownership: undefined });

    expect(title).not.toContain("Owned");
  });

  it("names the faction claiming a tile", () => {
    const title = renderWithTile({
      ...tile(3, 4, true),
      ownership: ownership({ status: "Claimed", claimedByPlayerIds: [2] }),
    });

    expect(title).toContain("Claimed by Carthage");
  });

  /**
   * Claims are the only non-exclusive state, so a tile several players assert names all of them - comma-separated, in the same
   * order their colours appear along the border's dashes.
   */
  it("lists every claimant of a contested tile, comma-separated", () => {
    const title = renderWithTile({
      ...tile(3, 4, true),
      ownership: ownership({ status: "Claimed", claimedByPlayerIds: [1, 2, 3] }),
    });

    expect(title).toContain("Claimed by Rome, Carthage, Numidia");
  });

  /** A claimant with no name in the lookup still has to appear, or the list would silently lose a claimant the border draws. */
  it("falls back to the player number for a claimant it has no name for", () => {
    const title = renderWithTile({
      ...tile(3, 4, true),
      ownership: ownership({ status: "Claimed", claimedByPlayerIds: [1, 99] }),
    });

    expect(title).toContain("Claimed by Rome, Player #99");
  });

  /**
   * Nobody *holds* a claimed tile, so the line is built from the claimant list rather than from the holder - which is null here,
   * and would otherwise reduce the line to a bare "Claimed".
   */
  it("names claimants even though a claimed tile has no holder", () => {
    const title = renderWithTile({
      ...tile(3, 4, true),
      ownership: ownership({ status: "Claimed", claimedByPlayerIds: [2], effectiveHolderPlayerId: null }),
    });

    expect(title).toContain("Claimed by Carthage");
    expect(title).not.toMatch(/Claimed\s*$/m);
  });
});
