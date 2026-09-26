import React from "react";
import type { TileCell } from "./TileTypes";
import RiverStrokeLayer, { type RiverStroke } from "@/components/map/RiverStrokeLayer";

export interface RiverOverlayProps {
  /** The grid, row-major (`tiles2d[y][x]`), as TileGrid holds it. */
  tiles2d: TileCell[][];
  width: number;
  height: number;
  /** The side of one tile in pixels, so the overlay lands exactly on the grid's edges. */
  tileSizePx: number;
}

/**
 * Picks out the rivers the player can see and hands them to {@link RiverStrokeLayer} to draw.
 *
 * **Each edge is drawn once.** Both tiles either side of a river report it, so a tile draws its South and East edges always, and
 * its North and West only where the neighbour that would otherwise draw them has no rivers to report — an unexplored tile, or the
 * edge of the grid. Drawing all four from every tile would double every interior stroke, which shows: two 6px lines a pixel apart
 * are not the same picture as one.
 *
 * That rule is what belongs here rather than in the shared layer: the map editor reads the two edges each tile owns, and knows
 * every tile, so it has no fog to reason about and nothing to deduplicate.
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

  return <RiverStrokeLayer strokes={strokes} widthPx={width * tileSizePx} heightPx={height * tileSizePx} />;
});

export default RiverOverlay;
