import React from "react";
import type { Geography } from "@/types/map/Geography";
import type { TileCell } from "./TileTypes";
import Tile from "./Tile";

export const TILE_SIZE_PX = 30;

export interface TileGridProps {
  width: number;
  height: number;
  tiles2d: TileCell[][];
  geographies: Geography[];
  onTileClick?: (tile: TileCell) => void;

  /**
   * Optional hooks you can use for tooltips / styling.
   */
  getTileTitle?: (tile: TileCell) => string;
  getTileStyle?: (tile: TileCell) => React.CSSProperties;
  tileClassName?: string;
}

export default function TileGrid({ width, height, tiles2d, geographies, onTileClick, getTileTitle, getTileStyle, tileClassName }: TileGridProps) {
  // Flatten row-major (y then x)
  const flat = React.useMemo(() => tiles2d.flat(), [tiles2d]);

  // Log geographies whenever they change
  React.useEffect(() => {
    // Pick the fields you care about so the table is readable
    console.table(
      geographies.map((g) => ({
        id: g.campaignMapGeographyType,
        name: g.name,
        relativeElevation: g.relativeElevation,
        movementCost: g.movementCost,
        mapEditorIcon: g.mapEditorIcon ?? null,
        isPaintable: g.isPaintableOnCampaignMap,
      }))
    );
  }, [geographies]);

  return (
    <div style={{ overflow: "auto", border: "1px solid rgba(0,0,0,0.125)" }}>
      <div
        style={{
          display: "grid",
          gridTemplateColumns: `repeat(${width}, ${TILE_SIZE_PX}px)`,
          gridAutoRows: `${TILE_SIZE_PX}px`,
          gap: 0, // crisp grid (no spacing)
        }}>
        {flat.map((t) => {
          const g = geographies.find((g) => g.campaignMapGeographyType === t.geographyTypeId);
          if (!g) {
            throw new Error(`No Geography found for geographyTypeId=${t.geographyTypeId} at (${t.x},${t.y})`);
          }

          return (
            <Tile
              key={`${t.x},${t.y}`}
              tile={t}
              geography={g}
              sizePx={TILE_SIZE_PX}
              onClick={onTileClick}
              getTitle={getTileTitle}
              getStyle={getTileStyle}
              className={tileClassName}
            />
          );
        })}
      </div>
    </div>
  );
}
