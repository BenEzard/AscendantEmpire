import { fireEvent, render } from "@testing-library/react";
import type { Geography } from "@/types/map/Geography";
import type { ObservedForceTile, TileCell } from "../TileTypes";
import TileGrid, { type TileGridProps } from "../TileGrid";

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
});
