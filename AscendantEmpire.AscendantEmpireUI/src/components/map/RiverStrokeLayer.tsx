import React from "react";
import type { RiverWidth } from "@/types/map/Rivers";

/** A width a river can actually be drawn at. `"None"` never reaches here: an edge with no river produces no stroke at all. */
export type DrawableRiverWidth = Exclude<RiverWidth, "None">;

/** One river edge, already in grid pixels. */
export interface RiverStroke {
  /** Unique within the layer. Both callers use `"x,y,side"`, since each edge is emitted exactly once. */
  key: string;
  x1: number;
  y1: number;
  x2: number;
  y2: number;
  width: DrawableRiverWidth;
}

/**
 * How thick each width is drawn, in pixels. Large is twice Medium, so which of the two a river is reads at a glance and without
 * having to compare it against a neighbouring one.
 */
export const RIVER_STROKE_PX: Record<DrawableRiverWidth, number> = {
  Medium: 3,
  Large: 6,
};

/** How much wider the casing under each stroke is drawn than the stroke itself, split over both sides. */
const CASING_EXTRA_PX = 2;

export interface RiverStrokeLayerProps {
  /** The edges to draw, in grid pixels. */
  strokes: readonly RiverStroke[];
  /** The size of the layer in pixels, which is the size of the grid it sits over. */
  widthPx: number;
  heightPx: number;
  /**
   * Drawn inside the same SVG, above the rivers. The editor puts its hover highlight here so it lands in the same coordinate space
   * as the strokes without a second absolutely-positioned layer to keep in step.
   */
  children?: React.ReactNode;
  /** Lets a caller distinguish its layer in tests. Defaults to the name the in-game map has always used. */
  testId?: string;
}

/**
 * Draws river edges as strokes over a tile grid.
 *
 * One SVG over the whole grid rather than borders on the tiles themselves. A river belongs to the *edge* between two tiles, not to
 * either of them, so drawing it on a tile would mean deciding which tile owns the line and then keeping both sides of every river
 * in agreement about its width — which is exactly the problem the server already solved by storing each edge once.
 *
 * Purely presentational: **which** edges to draw, and how to avoid drawing one twice, differ between the in-game map (four derived
 * edges per tile, with fog deciding what is known) and the map editor (the two edges each tile owns, all of them known), so each
 * works that out for itself and hands the answer here. What they share is how a river should look, which is all this owns.
 *
 * Nothing here is interactive, and the layer never swallows a click: whatever is underneath it still receives every pointer event.
 */
const RiverStrokeLayer = React.memo(function RiverStrokeLayer({ strokes, widthPx, heightPx, children, testId = "river-overlay" }: RiverStrokeLayerProps) {
  if (strokes.length === 0 && children == null) {
    return null;
  }

  return (
    <svg
      // Absolute over the grid, which is the first child of the caller's positioned overlay layer, so the two line up exactly and
      // the rivers scroll with the map.
      style={{ position: "absolute", left: 0, top: 0, pointerEvents: "none" }}
      width={widthPx}
      height={heightPx}
      aria-hidden="true"
      data-testid={testId}>
      {/* A pale casing under every stroke, so a river reads against dark water and pale ice alike — biome colours run the whole
          range, and no single blue is legible over all of them on its own. */}
      <g data-testid="river-casings" stroke="var(--campaign-river-casing)" strokeLinecap="square" fill="none">
        {strokes.map((stroke) => (
          <line key={stroke.key} x1={stroke.x1} y1={stroke.y1} x2={stroke.x2} y2={stroke.y2} strokeWidth={RIVER_STROKE_PX[stroke.width] + CASING_EXTRA_PX} />
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
            strokeWidth={RIVER_STROKE_PX[stroke.width]}
            data-river-edge={stroke.key}
            data-river-width={stroke.width}
          />
        ))}
      </g>
      {children}
    </svg>
  );
});

export default RiverStrokeLayer;
