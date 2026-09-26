import React from "react";
import type { TileCell } from "./TileTypes";
import type { RiverWidth } from "@/types/map/Rivers";

/** One river edge, already in grid pixels. */
interface RiverStroke {
  /** `"x,y,side"` — unique because each edge is emitted by exactly one tile. */
  key: string;
  x1: number;
  y1: number;
  x2: number;
  y2: number;
  /** Never `"None"` — an edge with no river produces no stroke at all, so there is nothing here to check for again when drawing. */
  width: Exclude<RiverWidth, "None">;
}

/**
 * How thick each width is drawn, in pixels. Large is twice Medium, so which of the two a river is reads at a glance and without
 * having to compare it against a neighbouring one.
 */
const STROKE_PX: Record<Exclude<RiverWidth, "None">, number> = {
  Medium: 3,
  Large: 6,
};

/** How much wider the casing under each stroke is drawn than the stroke itself, split over both sides. */
const CASING_EXTRA_PX = 2;

export interface RiverOverlayProps {
  /** The grid, row-major (`tiles2d[y][x]`), as TileGrid holds it. */
  tiles2d: TileCell[][];
  width: number;
  height: number;
  /** The side of one tile in pixels, so the overlay lands exactly on the grid's edges. */
  tileSizePx: number;
}

/**
 * Draws the rivers between tiles as strokes along the tile edges they run on.
 *
 * One SVG over the whole grid rather than borders on the tiles themselves. A river belongs to the *edge* between two tiles, not to
 * either of them, so drawing it on a tile would mean deciding which tile owns the line and then keeping both sides of every river
 * in agreement about its width — which is exactly the problem the server already solved by storing each edge once.
 *
 * **Each edge is drawn once.** Both tiles either side of a river report it, so a tile draws its South and East edges always, and
 * its North and West only where the neighbour that would otherwise draw them has no rivers to report — an unexplored tile, or the
 * edge of the grid. Drawing all four from every tile would double every interior stroke, which shows: two 6px lines a pixel apart
 * are not the same picture as one.
 *
 * Nothing here is interactive, and the layer never swallows a click: tiles underneath it still receive every pointer event.
 */
const RiverOverlay = React.memo(function RiverOverlay({ tiles2d, width, height, tileSizePx }: RiverOverlayProps) {
  const strokes = React.useMemo(() => {
    const collected: RiverStroke[] = [];

    for (const row of tiles2d) {
      for (const tile of row) {
        const rivers = tile.rivers;

        // No rivers at all means ground the player has never explored, or a grid that has no rivers in it — either way there is
        // nothing to draw and nothing to say about this tile's edges.
        if (rivers == null) {
          continue;
        }

        const left = tile.x * tileSizePx;
        const top = tile.y * tileSizePx;
        const right = left + tileSizePx;
        const bottom = top + tileSizePx;

        // The two edges this tile always draws, matching the server's storage: a tile owns its South and East.
        if (rivers.south !== "None") {
          collected.push({ key: `${tile.x},${tile.y},south`, x1: left, y1: bottom, x2: right, y2: bottom, width: rivers.south });
        }

        if (rivers.east !== "None") {
          collected.push({ key: `${tile.x},${tile.y},east`, x1: right, y1: top, x2: right, y2: bottom, width: rivers.east });
        }

        // The other two are the neighbours' South and East. Drawn here only when that neighbour will not draw them itself, which is
        // what puts a river on the boundary of explored ground without doubling any interior stroke.
        const above = tiles2d[tile.y - 1]?.[tile.x];
        const toLeft = row[tile.x - 1];

        if (rivers.north !== "None" && above?.rivers == null) {
          collected.push({ key: `${tile.x},${tile.y},north`, x1: left, y1: top, x2: right, y2: top, width: rivers.north });
        }

        if (rivers.west !== "None" && toLeft?.rivers == null) {
          collected.push({ key: `${tile.x},${tile.y},west`, x1: left, y1: top, x2: left, y2: bottom, width: rivers.west });
        }
      }
    }

    return collected;
  }, [tiles2d, tileSizePx]);

  if (strokes.length === 0) {
    return null;
  }

  return (
    <svg
      // Absolute over the grid, which is the first child of TileGrid's positioned overlay layer, so the two line up exactly and the
      // rivers scroll with the map.
      style={{ position: "absolute", left: 0, top: 0, pointerEvents: "none" }}
      width={width * tileSizePx}
      height={height * tileSizePx}
      aria-hidden="true"
      data-testid="river-overlay">
      {/* A pale casing under every stroke, so a river reads against dark water and pale ice alike — biome colours run the whole
          range, and no single blue is legible over all of them on its own. */}
      <g data-testid="river-casings" stroke="var(--campaign-river-casing)" strokeLinecap="square" fill="none">
        {strokes.map((stroke) => (
          <line key={stroke.key} x1={stroke.x1} y1={stroke.y1} x2={stroke.x2} y2={stroke.y2} strokeWidth={STROKE_PX[stroke.width] + CASING_EXTRA_PX} />
        ))}
      </g>
      <g data-testid="river-strokes" stroke="var(--campaign-river)" strokeLinecap="square" fill="none">
        {strokes.map((stroke) => (
          <line
            key={stroke.key}
            x1={stroke.x1}
            y1={stroke.y1}
            x2={stroke.x2}
            y2={stroke.y2}
            strokeWidth={STROKE_PX[stroke.width]}
            data-river-edge={stroke.key}
            data-river-width={stroke.width}
          />
        ))}
      </g>
    </svg>
  );
});

export default RiverOverlay;
