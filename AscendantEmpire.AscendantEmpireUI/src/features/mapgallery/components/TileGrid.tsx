// src/features/map-gallery/components/TileGrid.tsx
import React from "react";
import Tile from "./Tile";
import type { TileCell } from "../types/TileTypes";
import type { Geography } from "@/types/map/Geography";

export const TILE_SIZE_PX = 50;

// Static styles hoisted to module scope so they aren't re-allocated on every render.
const CONTAINER_STYLE: React.CSSProperties = { overflow: "auto", border: "1px solid rgba(0,0,0,0.125)" };
const FOOTER_STYLE: React.CSSProperties = { padding: "6px 10px", fontSize: 12, opacity: 0.8 };

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

const TileGrid = React.memo(function TileGrid({
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

  // Index geographies by their type id once per geographies change, so the per-tile
  // lookup below is an O(1) Map.get() instead of an O(geographies) Array.find() that
  // would otherwise run for every tile on every render (O(tiles × geographies)).
  const geographyByType = React.useMemo(() => {
    const m = new Map<number, Geography>();
    for (const g of geographies) {
      m.set(g.campaignMapGeographyType, g);
    }
    return m;
  }, [geographies]);

  // Grid template depends on width; memoize so the style object identity is stable
  // across renders where width hasn't changed.
  const gridStyle = React.useMemo<React.CSSProperties>(
    () => ({
      display: "grid",
      gridTemplateColumns: `repeat(${width}, ${TILE_SIZE_PX}px)`,
      gridAutoRows: `${TILE_SIZE_PX}px`,
      gap: 0, // crisp grid (no spacing)
    }),
    [width],
  );

  return (
    <div style={CONTAINER_STYLE} onMouseLeave={onTileHoverEnd}>
      <div style={gridStyle}>
        {flat.map((t) => {
          const g = t.geographyTypeId == null ? null : (geographyByType.get(t.geographyTypeId) ?? null);
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

      <div style={FOOTER_STYLE}>
        {width} × {height}
      </div>
    </div>
  );
});

export default TileGrid;
