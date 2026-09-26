import React from "react";
import type { TileCell } from "../types/TileTypes";
import RiverStrokeLayer, { RIVER_STROKE_PX, type RiverStroke } from "@/components/map/RiverStrokeLayer";
import { edgeLinePx, type TileEdgeRef } from "../tools/riverEdgePicking";

export interface RiverOverlayProps {
  /** The grid, row-major (`tiles2d[y][x]`), as TileGrid holds it. */
  tiles2d: TileCell[][];
  width: number;
  height: number;
  /** The side of one tile in pixels, so the overlay lands exactly on the grid's edges. */
  tileSizePx: number;
  /** The edge a click would act on right now, drawn as a marker over the rivers. Null when the River tool is off or the pointer has left. */
  hoveredEdge?: TileEdgeRef | null;
  /** Whether that click would erase the river rather than draw one. Drawn in its own colour, since the two do opposite things. */
  hoveredEdgeErases?: boolean;
}

/** How thick the hover marker is drawn. Between the two river widths, so it reads as a target rather than as a river of its own. */
const HOVER_STROKE_PX = (RIVER_STROKE_PX.Medium + RIVER_STROKE_PX.Large) / 2;

/**
 * Draws the rivers the author has painted, and marks the edge the next click would set.
 *
 * Simpler than the in-game overlay's job: the editor reads the two edges each tile *owns*, so every edge on the map is reported
 * exactly once already and there is no fog, no neighbour to consult, and nothing to deduplicate. Each tile draws its own South and
 * East, and that is the whole of it.
 *
 * The hover marker goes inside the same SVG so it shares the strokes' coordinate space, and it is drawn last so it sits over a
 * river already on that edge — which is what an author widening or erasing one needs to see.
 */
const RiverOverlay = React.memo(function RiverOverlay({ tiles2d, width, height, tileSizePx, hoveredEdge, hoveredEdgeErases }: RiverOverlayProps) {
  const strokes = React.useMemo(() => {
    const collected: RiverStroke[] = [];

    for (const row of tiles2d) {
      for (const tile of row) {
        const left = tile.x * tileSizePx;
        const top = tile.y * tileSizePx;
        const right = left + tileSizePx;
        const bottom = top + tileSizePx;

        if (tile.southRiver !== "None") {
          collected.push({ key: `${tile.x},${tile.y},south`, x1: left, y1: bottom, x2: right, y2: bottom, width: tile.southRiver });
        }

        if (tile.eastRiver !== "None") {
          collected.push({ key: `${tile.x},${tile.y},east`, x1: right, y1: top, x2: right, y2: bottom, width: tile.eastRiver });
        }
      }
    }

    return collected;
  }, [tiles2d, tileSizePx]);

  const hoverLine = hoveredEdge == null ? null : edgeLinePx(hoveredEdge, tileSizePx);

  return (
    <RiverStrokeLayer strokes={strokes} widthPx={width * tileSizePx} heightPx={height * tileSizePx} testId="map-editor-river-overlay">
      {hoverLine && (
        <line
          x1={hoverLine.x1}
          y1={hoverLine.y1}
          x2={hoverLine.x2}
          y2={hoverLine.y2}
          stroke={hoveredEdgeErases ? "var(--map-editor-river-hover-erase)" : "var(--map-editor-river-hover)"}
          strokeWidth={HOVER_STROKE_PX}
          strokeLinecap="round"
          data-testid="river-hover-marker"
          data-river-hover-edge={`${hoveredEdge!.x},${hoveredEdge!.y},${hoveredEdge!.side}`}
          data-river-hover-action={hoveredEdgeErases ? "erase" : "draw"}
        />
      )}
    </RiverStrokeLayer>
  );
});

export default RiverOverlay;
