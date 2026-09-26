import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import RiverOverlay from "../RiverOverlay";
import type { TileCell } from "../../types/TileTypes";
import type { RiverWidth } from "@/types/map/Rivers";

/**
 * The map editor's river overlay: the two edges each tile owns, drawn once each, plus the marker for the edge a click would set.
 *
 * Worth a component test rather than only a pure one, because the bug this guards against is visual — an edge drawn twice, or an
 * edge drawn from the wrong tile, looks almost right and reads as a rendering glitch rather than as wrong data.
 */
const TILE = 50;

function tile(x: number, y: number, southRiver: RiverWidth = "None", eastRiver: RiverWidth = "None"): TileCell {
  return { x, y, biomeTypeId: null, geographyTypeId: null, southRiver, eastRiver };
}

/** A 2 x 2 grid, with whatever rivers the caller puts on it. */
function grid(...tiles: TileCell[]): TileCell[][] {
  const rows: TileCell[][] = [
    [tile(0, 0), tile(1, 0)],
    [tile(0, 1), tile(1, 1)],
  ];

  for (const t of tiles) {
    rows[t.y][t.x] = t;
  }

  return rows;
}

function renderOverlay(tiles2d: TileCell[][], hoveredEdge?: Parameters<typeof RiverOverlay>[0]["hoveredEdge"], hoveredEdgeErases = false) {
  return render(
    <RiverOverlay tiles2d={tiles2d} width={2} height={2} tileSizePx={TILE} hoveredEdge={hoveredEdge} hoveredEdgeErases={hoveredEdgeErases} />,
  );
}

/** Every drawn edge, by the `"x,y,side"` key the stroke carries. */
function drawnEdges(): string[] {
  return Array.from(document.querySelectorAll("[data-river-edge]")).map((line) => line.getAttribute("data-river-edge")!);
}

describe("map editor RiverOverlay", () => {
  it("draws a tile's south and east rivers and nothing else", () => {
    renderOverlay(grid(tile(0, 0, "Medium", "Large")));

    expect(drawnEdges()).toEqual(["0,0,south", "0,0,east"]);
  });

  it("draws each edge once, so a river between two tiles is one line and not two", () => {
    // (0, 0)'s south edge is also (0, 1)'s north edge. Only the owner reports it, so only one stroke should appear.
    renderOverlay(grid(tile(0, 0, "Medium"), tile(0, 1, "Medium")));

    expect(drawnEdges()).toEqual(["0,0,south", "0,1,south"]);
  });

  it("draws Large at twice the thickness of Medium, which is the only thing telling them apart on the map", () => {
    renderOverlay(grid(tile(0, 0, "Medium", "Large")));

    const medium = document.querySelector('[data-river-edge="0,0,south"]')!;
    const large = document.querySelector('[data-river-edge="0,0,east"]')!;

    expect(Number(large.getAttribute("stroke-width"))).toBe(Number(medium.getAttribute("stroke-width")) * 2);
  });

  it("puts the hover marker on the edge a click would set, even where no river runs yet", () => {
    renderOverlay(grid(), { x: 1, y: 1, side: "north" });

    const marker = screen.getByTestId("river-hover-marker");

    // (1, 1)'s north edge is the line along the top of that tile: y = 50, from x = 50 to x = 100.
    expect(marker.getAttribute("y1")).toBe("50");
    expect(marker.getAttribute("y2")).toBe("50");
    expect(marker.getAttribute("x1")).toBe("50");
    expect(marker.getAttribute("x2")).toBe("100");
  });

  it("marks the hover as drawing or as erasing, since the tool toggles and the two do opposite things", () => {
    const { unmount } = renderOverlay(grid(), { x: 0, y: 0, side: "south" });
    expect(screen.getByTestId("river-hover-marker").getAttribute("data-river-hover-action")).toBe("draw");
    unmount();

    renderOverlay(grid(tile(0, 0, "Medium")), { x: 0, y: 0, side: "south" }, true);
    expect(screen.getByTestId("river-hover-marker").getAttribute("data-river-hover-action")).toBe("erase");
  });

  it("renders nothing at all for a map with no rivers and no hover, so an untouched map is untouched", () => {
    renderOverlay(grid());

    expect(screen.queryByTestId("map-editor-river-overlay")).toBeNull();
  });
});
