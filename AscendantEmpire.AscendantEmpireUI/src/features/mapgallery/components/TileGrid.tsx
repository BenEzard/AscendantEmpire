// src/features/map-gallery/components/TileGrid.tsx
import React from "react";
import Tile from "./Tile";
import type { TileCell } from "../types/TileTypes";
import type { Geography } from "@/types/map/Geography";

export const TILE_SIZE_PX = 30;

export interface TileGridProps {
  width: number;
  height: number;
  tiles2d: TileCell[][];
  geographies: Geography[];
  onTileClick?: (tile: TileCell) => void;
  onTileHover?: (tile: TileCell) => void;
  onTileHoverEnd?: () => void;

  /**
   * Optional hooks you can use for tooltips / styling.
   */
  getTileTitle?: (tile: TileCell) => string;
  getTileStyle?: (tile: TileCell) => React.CSSProperties;
  tileClassName?: string;
}

export default function TileGrid({
  width,
  height,
  tiles2d,
  geographies,
  onTileClick,
  onTileHover,
  onTileHoverEnd,
  getTileTitle,
  getTileStyle,
  tileClassName,
}: TileGridProps) {
  // Flatten row-major (y then x)
  const flat = React.useMemo(() => tiles2d.flat(), [tiles2d]);

  return (
    <div style={{ overflow: "auto", border: "1px solid rgba(0,0,0,0.125)" }} onMouseLeave={onTileHoverEnd}>
      <div
        style={{
          display: "grid",
          gridTemplateColumns: `repeat(${width}, ${TILE_SIZE_PX}px)`,
          gridAutoRows: `${TILE_SIZE_PX}px`,
          gap: 0, // crisp grid (no spacing)
        }}>
        {flat.map((t) => {
          const g = t.geographyTypeId == null ? null : (geographies.find((g) => g.campaignMapGeographyType === t.geographyTypeId) ?? null);
          if (t.geographyTypeId != null && !g) {
            throw new Error(`No Geography found for geographyTypeId=${t.geographyTypeId} at (${t.x},${t.y})`);
          }

          return (
            <Tile
              key={`${t.x},${t.y}`}
              tile={t}
              geography={g}
              sizePx={TILE_SIZE_PX}
              onClick={onTileClick}
              onHover={onTileHover}
              getTitle={getTileTitle}
              getStyle={getTileStyle}
              className={tileClassName}
            />
          );
        })}
      </div>

      <div style={{ padding: "6px 10px", fontSize: 12, opacity: 0.8 }}>
        {width} × {height}
      </div>
    </div>
  );
}
