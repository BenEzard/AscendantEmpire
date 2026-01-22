// src/features/map-gallery/components/Tile.tsx
import React from "react";
import type { Geography } from "@/types/map/Geography";
import type { TileCell } from "./TileTypes";
import { iconMap } from "./mapEditorIcons";

export interface TileProps {
  tile: TileCell;
  sizePx: number;
  geography: Geography;
  onClick?: (tile: TileCell) => void;
  className?: string;
  /**
   * Optional styling hooks.
   */
  getTitle?: (tile: TileCell) => string;
  getStyle?: (tile: TileCell) => React.CSSProperties;
}

const Tile = React.memo(function Tile({ tile, sizePx, geography, onClick, className, getTitle, getStyle }: TileProps) {
  const clickable = !!onClick;

  const baseStyle: React.CSSProperties = {
    width: sizePx,
    height: sizePx,
    display: "block",
    cursor: clickable ? "pointer" : "default",
    userSelect: "none",

    // no rounding
    borderRadius: 0,

    // base visuals; allow getStyle to override
    background: "rgba(0,0,0,0.03)",
    border: "1px solid rgba(0,0,0,0.12)",
  };

  const iconKey = geography?.mapEditorIcon ?? null;
  const Icon = iconKey ? iconMap[iconKey] : undefined;

  const style = { ...baseStyle, ...(getStyle?.(tile) ?? {}) };
  const combinedClassName = ["tile", className].filter(Boolean).join(" ");

  return (
    <div className={combinedClassName} role={clickable ? "button" : undefined} title={getTitle?.(tile)} onClick={() => onClick?.(tile)} style={style}>
      {Icon ? <Icon className="tile-icon-fill" aria-hidden /> : null}
    </div>
  );
});

export default Tile;
