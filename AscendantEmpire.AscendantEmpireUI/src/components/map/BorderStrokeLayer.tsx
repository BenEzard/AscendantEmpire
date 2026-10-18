import React from "react";

/** One border segment, already in grid pixels. */
export interface BorderStroke {
  /** Unique within the layer. The callers use `"x,y,side"`, since each tile emits each of its own sides at most once. */
  key: string;
  x1: number;
  y1: number;
  x2: number;
  y2: number;
  /**
   * The colours to draw it in, in the order they appear along the line.
   *
   * One for an ordinary border. Several for a tile claimed by several players, whose colours then take turns along the dashes —
   * claims are the only non-exclusive state, so this is the one case where a border belongs to more than one faction at once.
   */
  colours: readonly string[];
  /** Dashed for a claim, solid for ownership. A claim is an assertion rather than control, and the line says which it is. */
  isDashed: boolean;
}

/** How thick a border is drawn, in pixels. */
export const BORDER_STROKE_PX = 3;

/** How long one dash of a claim border is, in pixels, and how long the transparent gap after it is. */
const CLAIM_DASH_PX = 6;

/** How much wider the casing under each stroke is drawn than the stroke itself, split over both sides. */
const CASING_EXTRA_PX = 2;

export interface BorderStrokeLayerProps {
  /** The segments to draw, in grid pixels. */
  strokes: readonly BorderStroke[];
  /** The size of the layer in pixels, which is the size of the grid it sits over. */
  widthPx: number;
  heightPx: number;
  /** Lets a caller distinguish its layer in tests. */
  testId?: string;
}

/**
 * One drawn run of a stroke: a colour, and the dash pattern that places it along the line.
 *
 * A solid border is one run with no pattern. A claim is one run per claimant, each offset so that its dashes fall in the gaps
 * the others left.
 */
interface StrokeRun {
  key: string;
  colour: string;
  dashArray?: string;
  dashOffset?: number;
}

/**
 * Splits a stroke into the runs that draw it.
 *
 * A claim is dashed with **transparent gaps** — the terrain shows through, which is what distinguishes an assertion from real
 * control at a glance. With `n` claimants the cycle is `n` dashes and `n` gaps, so each claimant draws `CLAIM_DASH_PX` on and
 * `(2n - 1) * CLAIM_DASH_PX` off, shifted `2 * CLAIM_DASH_PX` per claimant. For one claimant that is a plain `6 6`; for two it
 * is colour, gap, colour, gap. The border therefore stays visibly dashed however many players claim the tile, which matters
 * because dashed-against-solid is the whole claim-against-owned signal.
 */
function toRuns(stroke: BorderStroke): StrokeRun[] {
  if (!stroke.isDashed) {
    // A solid border belongs to one player, so later colours would have nowhere to go. Only the first is drawn.
    return [{ key: stroke.key, colour: stroke.colours[0] }];
  }

  const claimantCount = stroke.colours.length;
  const dashArray = `${CLAIM_DASH_PX} ${CLAIM_DASH_PX * (2 * claimantCount - 1)}`;

  return stroke.colours.map((colour, index) => ({
    key: `${stroke.key},${index}`,
    colour,
    dashArray,
    // Negative, so run 0 starts at the beginning of the line and each later run starts one dash-and-gap further along.
    dashOffset: -(index * 2 * CLAIM_DASH_PX),
  }));
}

/**
 * Draws territory borders as strokes over a tile grid.
 *
 * One SVG over the whole grid rather than CSS borders on the tiles, for the same reason {@link RiverStrokeLayer} is one: a border
 * follows the boundary of a *region*, so which of a tile's sides carry one depends on its neighbours, and a per-tile border would
 * have to be kept in step with four other tiles' idea of the same line.
 *
 * Unlike a river, a border is **drawn once per side rather than once per edge**. Both sides of a frontier draw their own colour,
 * inset into their own tile, so a contested boundary reads as two colours meeting — which is the whole point of drawing it. The
 * caller insets the coordinates; this only decides how a border looks.
 *
 * **Every stroke is cased**, the same way a river is: a darker line drawn a little wider underneath it. A faction colour has to
 * sit on whatever biome the border happens to cross, and the map's colours run from pale sand to near-black water, so the same
 * indigo reads very differently from one tile to the next — the casing is what keeps a border's weight even across them.
 *
 * The casing carries its run's own dash pattern rather than being drawn solid, so a claim's gaps stay transparent. That does
 * fringe each dash rather than merely edging it, since a 6px mark has no long sides for the casing to run along; even weight
 * across biomes is the trade being made for it.
 *
 * Purely presentational, and never interactive: the layer swallows no pointer events, so the tiles underneath still receive every
 * click.
 */
const BorderStrokeLayer = React.memo(function BorderStrokeLayer({ strokes, widthPx, heightPx, testId = "ownership-overlay" }: BorderStrokeLayerProps) {
  const runsByStroke = React.useMemo(() => strokes.filter((stroke) => stroke.colours.length > 0).map((stroke) => ({ stroke, runs: toRuns(stroke) })), [strokes]);

  if (runsByStroke.length === 0) {
    return null;
  }

  return (
    <svg
      // Absolute over the grid, which is the first child of the caller's positioned overlay layer, so the two line up exactly and
      // the borders scroll with the map.
      style={{ position: "absolute", left: 0, top: 0, pointerEvents: "none" }}
      width={widthPx}
      height={heightPx}
      aria-hidden="true"
      data-testid={testId}>
      {/* The casing under every stroke, so a faction colour keeps the same weight over biomes that run from pale sand to
          near-black water. Each one carries its run's dash pattern rather than being solid, because a claim's gaps are meant to
          let the terrain through. */}
      <g data-testid="border-casings" stroke="var(--campaign-border-casing)" strokeLinecap="butt" fill="none">
        {runsByStroke.map(({ stroke, runs }) =>
          runs.map((run) => (
            <line
              key={run.key}
              x1={stroke.x1}
              y1={stroke.y1}
              x2={stroke.x2}
              y2={stroke.y2}
              strokeWidth={BORDER_STROKE_PX + CASING_EXTRA_PX}
              strokeDasharray={run.dashArray}
              strokeDashoffset={run.dashOffset}
            />
          )),
        )}
      </g>
      <g data-testid="border-strokes" strokeLinecap="butt" fill="none">
        {runsByStroke.map(({ stroke, runs }) =>
          runs.map((run) => (
            <line
              key={run.key}
              x1={stroke.x1}
              y1={stroke.y1}
              x2={stroke.x2}
              y2={stroke.y2}
              stroke={run.colour}
              strokeWidth={BORDER_STROKE_PX}
              strokeDasharray={run.dashArray}
              strokeDashoffset={run.dashOffset}
              data-border-edge={stroke.key}
              data-border-kind={stroke.isDashed ? "claim" : "owned"}
            />
          )),
        )}
      </g>
    </svg>
  );
});

export default BorderStrokeLayer;
