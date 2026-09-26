import { fireEvent, render } from "@testing-library/react";
import type { Geography } from "@/types/map/Geography";
import type { ObservedForceTile, TileCell } from "../TileTypes";
import TileGrid, { TILE_SIZE_PX, type TileGridProps } from "../TileGrid";
import type { TileRivers } from "@/types/map/Rivers";

/** How many times each tile has rendered, keyed by `"x,y"`. Hoisted alongside the mock below, which writes to it. */
const renderCounts = vi.hoisted(() => new Map<string, number>());

/*
 * Wraps the real Tile in a memoised spy that counts its renders. React.memo's default comparison is the same shallow one Tile's own memo
 * uses, so the spy renders exactly when the real tile would — which makes the counts a direct measure of what these tests are about.
 */
vi.mock("../Tile", async (importOriginal) => {
  const React = await import("react");
  const actual = await importOriginal<typeof import("../Tile")>();
  const RealTile = actual.default;

  const SpyTile = React.memo(function SpyTile(props: React.ComponentProps<typeof RealTile>) {
    const key = `${props.tile.x},${props.tile.y}`;
    renderCounts.set(key, (renderCounts.get(key) ?? 0) + 1);

    return React.createElement(RealTile, props);
  });

  return { ...actual, default: SpyTile };
});

function force(id: number): ObservedForceTile {
  return {
    id,
    recordType: 0,
    ownerPlayerId: 1,
    unitTemplateId: 3,
    healthyTroops: 10,
    injuredTroops: 0,
    initialStrength: 10,
    hasCampaignMovementRemaining: true,
    campaignMovementRemaining: 2,
    campaignMovementPoints: 2,
    battleMovementRemaining: null,
  };
}

function tile(x: number, forces?: ObservedForceTile[]): TileCell {
  return { x, y: 0, biomeTypeId: 1, geographyTypeId: 1, isExplored: true, forces };
}

// Everything the grid is given is held at module scope, so that between renders only the prop a test changes has a new identity.
const TILES_2D: TileCell[][] = [[tile(0), tile(1, [force(10)]), tile(2, [force(20)])]];
const GEOGRAPHIES = [{ campaignMapGeographyType: 1, name: "Plains" } as Geography];

// ObservedForce draws nothing without a picture, and the selected state is only visible on what it draws.
const IMAGES = new Map([[3, "unit.png"]]);

const BASE_PROPS: TileGridProps = {
  width: 3,
  height: 1,
  tiles2d: TILES_2D,
  geographies: GEOGRAPHIES,
  unitImageSrcByUnitTemplateId: IMAGES,
};

/** Renders the grid, and returns a function that re-renders it with some props replaced and the render counts cleared first. */
function renderGrid(props: Partial<TileGridProps>) {
  const view = render(<TileGrid {...BASE_PROPS} {...props} />);

  const update = (nextProps: Partial<TileGridProps>) => {
    renderCounts.clear();
    view.rerender(<TileGrid {...BASE_PROPS} {...nextProps} />);
  };

  return { ...view, update };
}

/** The tiles that rendered since the counts were last cleared, as `"x,y"` keys in order. */
function renderedTiles(): string[] {
  return [...renderCounts.keys()].sort();
}

beforeEach(() => {
  renderCounts.clear();
});

/**
 * Tile is memoised, but every tile on the map used to be handed the same callbacks and the same selection set. Any change to the selection gave those a
 * new identity, so all ~2,500 tiles re-rendered for a change that touched one or two of them. The grid now hands each tile plain values and handlers
 * that never change, and these pin that only the tiles whose drawing changed are re-rendered.
 */
describe("TileGrid re-rendering", () => {
  it("re-renders only the tiles that gain or lose the highlight when it moves", () => {
    const { update } = renderGrid({ isTileHighlighted: (t) => t.x === 0 });

    update({ isTileHighlighted: (t) => t.x === 1 });

    expect(renderedTiles()).toEqual(["0,0", "1,0"]);
  });

  /** A caller's click handler depends on the selection, so it is replaced on every click — which must not reach the tiles. */
  it("re-renders no tiles when the click and hold handlers are replaced, and a click still reaches the newest handler", () => {
    const firstClick = vi.fn();
    const { container, update } = renderGrid({ onTileClick: firstClick, onTileHold: vi.fn() });

    const latestClick = vi.fn();
    update({ onTileClick: latestClick, onTileHold: vi.fn() });

    expect(renderedTiles()).toEqual([]);

    fireEvent.click(container.querySelectorAll(".tile")[1]);

    expect(firstClick).not.toHaveBeenCalled();
    expect(latestClick).toHaveBeenCalledWith(TILES_2D[0][1]);
  });

  /** Clicking an empty tile resets the selection to a new empty array, and so a new set, even though nothing about the selection changed. */
  it("re-renders no tiles when the selection set is replaced with one holding the same forces", () => {
    const { update } = renderGrid({ selectedForceIds: new Set([10]) });

    update({ selectedForceIds: new Set([10]) });

    expect(renderedTiles()).toEqual([]);
  });

  it("re-renders only the tile whose force joined the selection, and draws that force as selected", () => {
    const { container, update } = renderGrid({ selectedForceIds: new Set([10]) });

    update({ selectedForceIds: new Set([10, 20]) });

    expect(renderedTiles()).toEqual(["2,0"]);
    expect(container.querySelectorAll(".observed-force--selected")).toHaveLength(2);
  });

  /** The observed set is rebuilt after every step, which gives the background callback a new identity even where a tile's fog did not change. */
  it("re-renders only the tile whose background changed when the background callback is replaced", () => {
    const { update } = renderGrid({ getTileBackground: (t) => (t.x === 2 ? "rgb(0, 0, 255)" : "rgb(255, 0, 0)") });

    update({ getTileBackground: (t) => (t.x === 1 ? "rgb(0, 0, 255)" : "rgb(255, 0, 0)") });

    expect(renderedTiles()).toEqual(["1,0", "2,0"]);
  });

  /** The hover handler is built on the page's selection-dependent state too, so replacing it must not reach the tiles either. */
  it("re-renders no tiles when the hover handler is replaced, and hovering still reaches the newest handler", () => {
    const firstHover = vi.fn();
    const { container, update } = renderGrid({ onTileHover: firstHover });

    const latestHover = vi.fn();
    update({ onTileHover: latestHover });

    expect(renderedTiles()).toEqual([]);

    fireEvent.pointerEnter(container.querySelectorAll(".tile")[2]);

    expect(firstHover).not.toHaveBeenCalled();
    expect(latestHover).toHaveBeenCalledWith(TILES_2D[0][2]);
  });
});

describe("TileGrid hover", () => {
  /** Only the tile entered is reported while moving across the map, and null only once the pointer leaves the map altogether — never a null between tiles. */
  it("reports each tile entered, and null when the pointer leaves the map", () => {
    const onTileHover = vi.fn();
    const { container } = renderGrid({ onTileHover });

    const tiles = container.querySelectorAll(".tile");
    // Moving from one tile to the next never leaves the grid's layer in a browser, so only the enters are fired here.
    fireEvent.pointerEnter(tiles[0]);
    fireEvent.pointerEnter(tiles[1]);

    expect(onTileHover.mock.calls).toEqual([[TILES_2D[0][0]], [TILES_2D[0][1]]]);

    fireEvent.pointerLeave(tiles[1].parentElement!.parentElement!);

    expect(onTileHover).toHaveBeenLastCalledWith(null);
  });

  it("draws its children over the tiles", () => {
    const { getByTestId } = render(
      <TileGrid {...BASE_PROPS}>
        <span data-testid="overlay" />
      </TileGrid>,
    );

    expect(getByTestId("overlay")).toBeInTheDocument();
  });
});

/** Builds a tile with the river edges given; anything left out has no river. Omit `rivers` entirely for ground the player has not explored. */
function riverTile(x: number, y: number, rivers?: Partial<TileRivers>): TileCell {
  return {
    x,
    y,
    biomeTypeId: 1,
    geographyTypeId: 1,
    isExplored: rivers != null,
    rivers: rivers == null ? undefined : { north: "None", east: "None", south: "None", west: "None", ...rivers },
  };
}

/**
 * A 2 x 2 map with two rivers on it, and one unexplored tile.
 *
 * - The edge between (0, 0) and (1, 0) is a Large river. Both tiles report it — (0, 0) as its East, (1, 0) as its West.
 * - The edge between (0, 0) and (0, 1) is a Medium river, and (0, 1) is unexplored, so only (0, 0) reports it.
 * - The edge between (0, 1) and (1, 1) is a Medium river reported only by (1, 1), because the tile to its left is unexplored.
 */
const RIVER_TILES_2D: TileCell[][] = [
  [riverTile(0, 0, { east: "Large", south: "Medium" }), riverTile(1, 0, { west: "Large" })],
  [riverTile(0, 1), riverTile(1, 1, { west: "Medium" })],
];

const RIVER_PROPS: TileGridProps = { ...BASE_PROPS, width: 2, height: 2, tiles2d: RIVER_TILES_2D };

/** Every river stroke the grid drew, as `"x,y,side"` keys paired with the width drawn. */
function drawnRivers(container: HTMLElement): { edge: string; width: string; strokeWidth: string }[] {
  return [...container.querySelectorAll("[data-river-edge]")].map((line) => ({
    edge: line.getAttribute("data-river-edge")!,
    width: line.getAttribute("data-river-width")!,
    strokeWidth: line.getAttribute("stroke-width")!,
  }));
}

/**
 * Rivers run between tiles, so both tiles either side of one report it and the grid has to decide which of them draws it. Drawing
 * all four edges of every tile would lay a second line over every interior river — two thick strokes a pixel apart, not one river.
 */
describe("TileGrid rivers", () => {
  it("draws each edge once, from the tile that owns it", () => {
    const { container } = render(<TileGrid {...RIVER_PROPS} />);

    // (1, 0) reports the Large river on its West, and does not draw it: (0, 0) already drew the same edge as its East.
    expect(drawnRivers(container).map((stroke) => stroke.edge).sort()).toEqual(["0,0,east", "0,0,south", "1,1,west"]);
  });

  it("draws a north or west edge whose neighbour is unexplored, since that neighbour reports nothing to draw it from", () => {
    const { container } = render(<TileGrid {...RIVER_PROPS} />);

    // (0, 1) is unexplored, so the edge between it and (1, 1) reaches the client only on (1, 1)'s West.
    expect(drawnRivers(container).map((stroke) => stroke.edge)).toContain("1,1,west");
  });

  it("draws a large river noticeably thicker than a medium one", () => {
    const { container } = render(<TileGrid {...RIVER_PROPS} />);

    const strokes = drawnRivers(container);
    const large = Number(strokes.find((stroke) => stroke.width === "Large")!.strokeWidth);
    const medium = Number(strokes.find((stroke) => stroke.width === "Medium")!.strokeWidth);

    expect(large).toBeGreaterThan(medium);
  });

  it("puts every stroke on the boundary between two tiles, never across a tile", () => {
    const { container } = render(<TileGrid {...RIVER_PROPS} />);

    // An edge is either horizontal or vertical, and lies on a multiple of the tile size — which is what "between tiles" means in pixels.
    for (const line of container.querySelectorAll("[data-river-edge]")) {
      const [x1, y1, x2, y2] = ["x1", "y1", "x2", "y2"].map((attribute) => Number(line.getAttribute(attribute)));

      expect(x1 === x2 || y1 === y2).toBe(true);
      expect([x1, y1, x2, y2].every((coordinate) => coordinate % TILE_SIZE_PX === 0)).toBe(true);
    }
  });

  it("draws nothing at all on a grid whose tiles have no rivers", () => {
    const { container } = render(<TileGrid {...BASE_PROPS} />);

    expect(container.querySelector("[data-testid='river-overlay']")).toBeNull();
  });
});
