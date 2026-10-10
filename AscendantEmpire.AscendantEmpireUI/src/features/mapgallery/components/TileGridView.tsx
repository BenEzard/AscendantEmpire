// src/features/map-gallery/components/TileGridView.tsx
import type { Geography } from "@/types/map/Geography";
import type { BiomeType } from "@/types/map/Biome";
import type { CampaignMapGeographyType } from "@/types/map/Geography";

export type TileCell = {
  x: number;
  y: number;
  biomeTypeId: BiomeType | null;
  geographyTypeId: CampaignMapGeographyType | null;
};

export interface TileGridViewProps {
  width: number;
  height: number;
  tiles2d: TileCell[][];
  geographies: Geography[];
  onTileClick?: (tile: TileCell) => void;

  // optional sizing
  tileSizePx?: number;
}

export default function TileGridView({ width, height, tiles2d, geographies, onTileClick, tileSizePx = 22 }: TileGridViewProps) {
  void geographies;
  return (
    <div style={{ overflow: "auto", border: "1px solid rgba(0,0,0,0.125)", borderRadius: 6 }}>
      <div
        style={{
          display: "grid",
          gridTemplateColumns: `repeat(${width}, ${tileSizePx}px)`,
          gridAutoRows: `${tileSizePx}px`,
          gap: 1,
          padding: 8,
        }}>
        {tiles2d.flat().map((t) => (
          <div
            key={`${t.x},${t.y}`}
            title={`(${t.x},${t.y}) biome=${t.biomeTypeId ?? "null"} geo=${t.geographyTypeId ?? "null"}`}
            onClick={() => onTileClick?.(t)}
            style={{
              width: tileSizePx,
              height: tileSizePx,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              fontSize: 10,
              userSelect: "none",
              cursor: onTileClick ? "pointer" : "default",
              background: "rgba(0,0,0,0.03)",
              border: "1px solid rgba(0,0,0,0.07)",
              borderRadius: 4,
            }}>
            {/* Minimal debug display */}
            {t.biomeTypeId ?? "·"}/{t.geographyTypeId ?? "·"}
          </div>
        ))}
      </div>

      <div style={{ padding: "6px 10px", fontSize: 12, opacity: 0.8 }}>
        {width} × {height}
      </div>
    </div>
  );
}
