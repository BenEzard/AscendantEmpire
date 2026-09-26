import { describe, expect, it } from "vitest";
import {
  currentWidthOf,
  edgeLinePx,
  isSameEdge,
  resolveEdgeFromPoint,
  toCanonicalEdge,
  type MapEdgeGeometry,
  type TileEdgeRef,
} from "../riverEdgePicking";
import type { RiverWidth } from "@/types/map/Rivers";

/**
 * Picking a river edge from a click, and translating it to the tile that owns it.
 *
 * Pure geometry, which is the part worth pinning: getting the ownership or the wrap wrong writes a river along the wrong border,
 * and getting the nearest-edge maths wrong makes the tool feel broken near a corner.
 *
 * The map is 4 x 3 with 50px tiles unless a test says otherwise, so there is an interior tile with a whole ring around it and
 * edges that face off the map on every side.
 */
const TILE = 50;
const WIDTH = 4;
const HEIGHT = 3;

const noWrap: MapEdgeGeometry = { width: WIDTH, height: HEIGHT, horizontalWrap: false, verticalWrap: false };
const wrapsBoth: MapEdgeGeometry = { width: WIDTH, height: HEIGHT, horizontalWrap: true, verticalWrap: true };

/** A point inside tile (x, y), offset from its top-left corner. */
function inTile(x: number, y: number, withinX: number, withinY: number): [number, number] {
  return [x * TILE + withinX, y * TILE + withinY];
}

describe("resolveEdgeFromPoint", () => {
  it("picks the north edge for a point near the top of a tile", () => {
    expect(resolveEdgeFromPoint(...inTile(1, 1, 25, 3), TILE, WIDTH, HEIGHT)).toEqual<TileEdgeRef>({ x: 1, y: 1, side: "north" });
  });

  it("picks the south edge for a point near the bottom of a tile", () => {
    expect(resolveEdgeFromPoint(...inTile(1, 1, 25, 47), TILE, WIDTH, HEIGHT)).toEqual<TileEdgeRef>({ x: 1, y: 1, side: "south" });
  });

  it("picks the west edge for a point near the left of a tile", () => {
    expect(resolveEdgeFromPoint(...inTile(1, 1, 3, 25), TILE, WIDTH, HEIGHT)).toEqual<TileEdgeRef>({ x: 1, y: 1, side: "west" });
  });

  it("picks the east edge for a point near the right of a tile", () => {
    expect(resolveEdgeFromPoint(...inTile(1, 1, 47, 25), TILE, WIDTH, HEIGHT)).toEqual<TileEdgeRef>({ x: 1, y: 1, side: "east" });
  });

  it("picks by the nearer side in a corner, not by the quadrant alone", () => {
    // Both of these sit in the tile's top-left quarter, but one is nearer the top and the other nearer the left.
    expect(resolveEdgeFromPoint(...inTile(2, 1, 20, 5), TILE, WIDTH, HEIGHT)?.side).toBe("north");
    expect(resolveEdgeFromPoint(...inTile(2, 1, 5, 20), TILE, WIDTH, HEIGHT)?.side).toBe("west");
  });

  it("reports the tile the point is in, not a neighbouring one", () => {
    expect(resolveEdgeFromPoint(...inTile(3, 2, 25, 47), TILE, WIDTH, HEIGHT)).toEqual<TileEdgeRef>({ x: 3, y: 2, side: "south" });
  });

  it("returns null for a point outside the grid, which is what a pointer leaving the layer gives", () => {
    expect(resolveEdgeFromPoint(-1, 10, TILE, WIDTH, HEIGHT)).toBeNull();
    expect(resolveEdgeFromPoint(10, -1, TILE, WIDTH, HEIGHT)).toBeNull();
    expect(resolveEdgeFromPoint(WIDTH * TILE, 10, TILE, WIDTH, HEIGHT)).toBeNull();
    expect(resolveEdgeFromPoint(10, HEIGHT * TILE, TILE, WIDTH, HEIGHT)).toBeNull();
  });
});

describe("toCanonicalEdge", () => {
  it("passes a tile's own south and east edges through unchanged", () => {
    expect(toCanonicalEdge({ x: 1, y: 1, side: "south" }, noWrap)).toEqual({ x: 1, y: 1, edge: "South" });
    expect(toCanonicalEdge({ x: 1, y: 1, side: "east" }, noWrap)).toEqual({ x: 1, y: 1, edge: "East" });
  });

  it("translates a north click to the south edge of the tile above, which is the row that owns it", () => {
    expect(toCanonicalEdge({ x: 1, y: 1, side: "north" }, noWrap)).toEqual({ x: 1, y: 0, edge: "South" });
  });

  it("translates a west click to the east edge of the tile to the left", () => {
    expect(toCanonicalEdge({ x: 1, y: 1, side: "west" }, noWrap)).toEqual({ x: 0, y: 1, edge: "East" });
  });

  it("gives a north click on the top row and a south click on the row above it the same owner", () => {
    expect(toCanonicalEdge({ x: 2, y: 1, side: "north" }, noWrap)).toEqual(toCanonicalEdge({ x: 2, y: 0, side: "south" }, noWrap));
  });

  it("refuses the edges that face off a map which does not wrap", () => {
    expect(toCanonicalEdge({ x: 0, y: 0, side: "north" }, noWrap)).toBeNull();
    expect(toCanonicalEdge({ x: 0, y: 0, side: "west" }, noWrap)).toBeNull();
    expect(toCanonicalEdge({ x: WIDTH - 1, y: 0, side: "east" }, noWrap)).toBeNull();
    expect(toCanonicalEdge({ x: 0, y: HEIGHT - 1, side: "south" }, noWrap)).toBeNull();
  });

  it("wraps the same edges round to the far side of a map that wraps on that axis", () => {
    expect(toCanonicalEdge({ x: 0, y: 0, side: "north" }, wrapsBoth)).toEqual({ x: 0, y: HEIGHT - 1, edge: "South" });
    expect(toCanonicalEdge({ x: 0, y: 0, side: "west" }, wrapsBoth)).toEqual({ x: WIDTH - 1, y: 0, edge: "East" });
    expect(toCanonicalEdge({ x: WIDTH - 1, y: 0, side: "east" }, wrapsBoth)).toEqual({ x: WIDTH - 1, y: 0, edge: "East" });
    expect(toCanonicalEdge({ x: 0, y: HEIGHT - 1, side: "south" }, wrapsBoth)).toEqual({ x: 0, y: HEIGHT - 1, edge: "South" });
  });

  it("wraps only the axis that wraps", () => {
    const wrapsHorizontallyOnly: MapEdgeGeometry = { width: WIDTH, height: HEIGHT, horizontalWrap: true, verticalWrap: false };

    expect(toCanonicalEdge({ x: 0, y: 0, side: "west" }, wrapsHorizontallyOnly)).toEqual({ x: WIDTH - 1, y: 0, edge: "East" });
    expect(toCanonicalEdge({ x: 0, y: 0, side: "north" }, wrapsHorizontallyOnly)).toBeNull();
  });

  it("returns null for a tile that is not on the map", () => {
    expect(toCanonicalEdge({ x: WIDTH, y: 0, side: "south" }, noWrap)).toBeNull();
    expect(toCanonicalEdge({ x: 0, y: HEIGHT, side: "east" }, noWrap)).toBeNull();
  });
});

describe("edgeLinePx", () => {
  it("draws each side along the boundary of its tile", () => {
    expect(edgeLinePx({ x: 1, y: 2, side: "north" }, TILE)).toEqual({ x1: 50, y1: 100, x2: 100, y2: 100 });
    expect(edgeLinePx({ x: 1, y: 2, side: "south" }, TILE)).toEqual({ x1: 50, y1: 150, x2: 100, y2: 150 });
    expect(edgeLinePx({ x: 1, y: 2, side: "west" }, TILE)).toEqual({ x1: 50, y1: 100, x2: 50, y2: 150 });
    expect(edgeLinePx({ x: 1, y: 2, side: "east" }, TILE)).toEqual({ x1: 100, y1: 100, x2: 100, y2: 150 });
  });

  it("draws a tile's north edge and the tile above's south edge on the same line", () => {
    expect(edgeLinePx({ x: 1, y: 2, side: "north" }, TILE)).toEqual(edgeLinePx({ x: 1, y: 1, side: "south" }, TILE));
  });
});

describe("currentWidthOf", () => {
  /** A 2 x 2 grid carrying only the river columns this helper reads. */
  function grid(overrides: Partial<Record<string, { southRiver: RiverWidth; eastRiver: RiverWidth }>> = {}) {
    return [0, 1].map((y) =>
      [0, 1].map((x) => overrides[`${x},${y}`] ?? { southRiver: "None" as RiverWidth, eastRiver: "None" as RiverWidth }),
    );
  }

  it("reads the width off the tile that owns the edge", () => {
    const tiles2d = grid({ "1,0": { southRiver: "Medium", eastRiver: "Large" } });

    expect(currentWidthOf(tiles2d, { x: 1, y: 0, edge: "South" })).toBe("Medium");
    expect(currentWidthOf(tiles2d, { x: 1, y: 0, edge: "East" })).toBe("Large");
  });

  it("reports None for an edge with no river, which is what makes a first click draw rather than erase", () => {
    expect(currentWidthOf(grid(), { x: 0, y: 0, edge: "South" })).toBe("None");
  });

  it("reports None for a grid that is not loaded, rather than throwing on a click that arrives early", () => {
    expect(currentWidthOf(null, { x: 0, y: 0, edge: "South" })).toBe("None");
    expect(currentWidthOf(grid(), { x: 9, y: 9, edge: "East" })).toBe("None");
  });
});

describe("isSameEdge", () => {
  it("is true only for the same side of the same tile", () => {
    expect(isSameEdge({ x: 1, y: 1, side: "north" }, { x: 1, y: 1, side: "north" })).toBe(true);
    expect(isSameEdge({ x: 1, y: 1, side: "north" }, { x: 1, y: 1, side: "south" })).toBe(false);
    expect(isSameEdge({ x: 1, y: 1, side: "north" }, { x: 2, y: 1, side: "north" })).toBe(false);
  });

  it("treats two absent edges as the same, so a pointer staying off the grid is not a change", () => {
    expect(isSameEdge(null, null)).toBe(true);
    expect(isSameEdge(null, { x: 1, y: 1, side: "north" })).toBe(false);
  });
});
