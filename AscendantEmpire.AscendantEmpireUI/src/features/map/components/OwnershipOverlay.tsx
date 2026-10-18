import React from "react";
import type { TileCell } from "./TileTypes";
import BorderStrokeLayer from "@/components/map/BorderStrokeLayer";
import { collectBorderStrokes, type OwnershipBandContext } from "@/features/map/util/ownershipBands";

export interface OwnershipOverlayProps {
  /** The grid, row-major (`tiles2d[y][x]`), as TileGrid holds it. */
  tiles2d: TileCell[][];
  width: number;
  height: number;
  /** The side of one tile in pixels, so the overlay lands exactly on the grid's edges. */
  tileSizePx: number;
  /** Each player's faction colour, which is what a border is drawn in — every claimant's as well as every owner's. */
  factionColourByPlayerId: ReadonlyMap<number, string>;
  /** Whether an observer can see the tile right now. A border is drawn only under live sight — see {@link getOwnershipBand}. */
  isTileObserved: (tile: TileCell) => boolean;
}

/**
 * Works out which tile sides carry a territory border and hands them to {@link BorderStrokeLayer} to draw.
 *
 * The counterpart of {@link RiverOverlay}, and split the same way: *which* sides to draw is the map's business and lives here,
 * while *how a border looks* is shared and lives in the layer. The rule itself is pure and sits in `ownershipBands`, so the
 * precedence between owned and claimed ground is testable without rendering anything.
 */
const OwnershipOverlay = React.memo(function OwnershipOverlay({
  tiles2d,
  width,
  height,
  tileSizePx,
  factionColourByPlayerId,
  isTileObserved,
}: OwnershipOverlayProps) {
  const strokes = React.useMemo(() => {
    const context: OwnershipBandContext = { factionColourByPlayerId, isTileObserved };

    return collectBorderStrokes(tiles2d, tileSizePx, context);
  }, [tiles2d, tileSizePx, factionColourByPlayerId, isTileObserved]);

  return <BorderStrokeLayer strokes={strokes} widthPx={width * tileSizePx} heightPx={height * tileSizePx} />;
});

export default OwnershipOverlay;
