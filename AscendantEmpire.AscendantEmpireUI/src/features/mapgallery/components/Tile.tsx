// src/features/map-gallery/components/Tile.tsx
import React from "react";
import { BiLocationPlus } from "react-icons/bi";
import type { TileCell } from "../types/TileTypes";
import { iconMap } from "./mapEditorIcons";
import type { Geography } from "@/types/map/Geography";
import { cssColorToRgba, pickTextColorOnBg } from "@/util/color";

export interface TileProps {
  tile: TileCell;
  sizePx: number;
  geography: Geography | null;
  onClick?: (tile: TileCell) => void;
  onHover?: (tile: TileCell) => void;
  className?: string;
  /** When true, renders a start-position marker icon above the geography icon. */
  showStartPosition?: boolean;
  /**
   * Optional styling hooks.
   */
  getTitle?: (tile: TileCell) => string;
  getStyle?: (tile: TileCell) => React.CSSProperties;
}

const Tile = React.memo(function Tile({ tile, sizePx, geography, onClick, onHover, className, showStartPosition, getTitle, getStyle }: TileProps) {
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

  const bgCandidate =
    (typeof style.background === "string" ? style.background : undefined) ??
    (typeof style.backgroundColor === "string" ? style.backgroundColor : undefined);
  const rgba = cssColorToRgba(bgCandidate);
  const bgForContrast = rgba && rgba.a < 0.2 ? "#ffffff" : bgCandidate;
  const iconColor = pickTextColorOnBg(bgForContrast);

  return (
    <div
      className={combinedClassName}
      role={clickable ? "button" : undefined}
      title={getTitle?.(tile)}
      onClick={() => onClick?.(tile)}
      onMouseEnter={() => onHover?.(tile)}
      style={style}>
      {Icon ? <Icon className="tile-icon-fill" aria-hidden style={{ color: iconColor }} /> : null}
      {showStartPosition ? <BiLocationPlus className="tile-start-position-icon" aria-hidden /> : null}
    </div>
  );
});

export default Tile;
