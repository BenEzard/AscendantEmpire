import React from "react";
import type { TileCell } from "./TileTypes";

export interface SettlementLabelOverlayProps {
  /** The grid, row-major (`tiles2d[y][x]`), as TileGrid holds it. */
  tiles2d: TileCell[][];
  width: number;
  height: number;
  /** The side of one tile in pixels, so a label lands exactly under the tile its settlement stands on. */
  tileSizePx: number;
}

/** One settlement's name and where on the grid it belongs, in pixels from the grid's top-left. */
interface SettlementLabel {
  /** The structure instance's id — stable across renders, unlike the tile coordinates. */
  key: number;
  name: string;
  /** The centre of the tile the settlement stands on. */
  centreXPx: number;
  /** The tile's bottom edge. The label is pulled back above it, so it sits inside the tile it names. */
  bottomYPx: number;
}

/**
 * Draws every visible settlement's name over the grid.
 *
 * <b>Why an overlay rather than a child of the tile.</b> A name is almost always wider than the 50px tile its settlement
 * stands on, and `.tile` sets `overflow: hidden` to keep sprites inside their own square — so a name drawn inside a tile is
 * clipped to the first few letters of itself. Each tile is also its own stacking context, which put the name under any unit
 * standing on the same ground. Drawn here, over the whole grid, it is clipped by nothing and covered by nothing.
 *
 * The counterpart of {@link RiverOverlay} and {@link OwnershipOverlay}, and here for the same reason they are: what it draws
 * belongs to the map rather than to any one tile.
 *
 * Only settlements are labelled. A watch tower is a kind, not a place, so it has no name of its own; a tile the player cannot
 * see reports no structures at all, so nothing filters for sight here.
 */
const SettlementLabelOverlay = React.memo(function SettlementLabelOverlay({
  tiles2d,
  width,
  height,
  tileSizePx,
}: SettlementLabelOverlayProps) {
  const labels = React.useMemo(() => {
    const collected: SettlementLabel[] = [];

    for (const row of tiles2d) {
      for (const tile of row) {
        for (const structure of tile.structures ?? []) {
          if (structure.settlementName == null) {
            continue;
          }

          collected.push({
            key: structure.id,
            name: structure.settlementName,
            centreXPx: (tile.x + 0.5) * tileSizePx,
            bottomYPx: (tile.y + 1) * tileSizePx,
          });
        }
      }
    }

    return collected;
  }, [tiles2d, tileSizePx]);

  if (labels.length === 0) {
    return null;
  }

  return (
    <div
      className="settlement-labels"
      style={{ width: width * tileSizePx, height: height * tileSizePx }}
      aria-hidden>
      {labels.map((label) => (
        <span
          key={label.key}
          className="settlement-label"
          // Positioned rather than classed, because the coordinates are per-settlement and there is nothing to name them.
          style={{ left: label.centreXPx, top: label.bottomYPx }}>
          {label.name}
        </span>
      ))}
    </div>
  );
});

export default SettlementLabelOverlay;
