// src/features/map-gallery/components/Tile.tsx
import React from "react";
import { BiLocationPlus } from "react-icons/bi";
import type { TileCell } from "../types/TileTypes";
import { iconMap } from "./mapEditorIcons";
import type { Geography } from "@/types/map/Geography";
import { cssColorToRgba, pickTextColorOnBg } from "@/util/color";
import MapResourceIcon from "./mapResources/MapResourceIcon";

/** A MapResource placed on the tile, as the tile draws it. */
export type TileMapResourceMarker = {
  key: number;
  name: string;
  iconPath: string | null;
};

export interface TileProps {
  tile: TileCell;
  sizePx: number;
  geography: Geography | null;
  onClick?: (tile: TileCell) => void;
  onHover?: (tile: TileCell) => void;
  className?: string;
  /** When true, renders a start-position marker icon above the geography icon. */
  showStartPosition?: boolean;
  /** MapResources placed on the tile, drawn in its bottom-right corner in slot order. */
  mapResources?: TileMapResourceMarker[];
  /**
   * Optional styling hooks.
   */
  getTitle?: (tile: TileCell) => string;
  getStyle?: (tile: TileCell) => React.CSSProperties;
}

const Tile = React.memo(function Tile({
  tile,
  sizePx,
  geography,
  onClick,
  onHover,
  className,
  showStartPosition,
  mapResources,
  getTitle,
  getStyle,
}: TileProps) {
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

  // The hover title names the tile's MapResources after whatever the caller's title says.
  const baseTitle = getTitle?.(tile);
  const assetTitle = mapResources && mapResources.length > 0 ? `MapResources: ${mapResources.map((m) => m.name).join(", ")}` : undefined;
  const title = [baseTitle, assetTitle].filter(Boolean).join("\n") || undefined;

  return (
    <div
      className={combinedClassName}
      role={clickable ? "button" : undefined}
      title={title}
      onClick={() => onClick?.(tile)}
      onMouseEnter={() => onHover?.(tile)}
      style={style}>
      {Icon ? <Icon className="tile-icon-fill" aria-hidden style={{ color: iconColor }} /> : null}
      {showStartPosition ? <BiLocationPlus className="tile-start-position-icon" aria-hidden /> : null}
      {mapResources && mapResources.length > 0 ? (
        <span className="tile-map-resources" aria-hidden>
          {mapResources.map((marker) => (
            <MapResourceIcon key={marker.key} iconPath={marker.iconPath} size={Math.round(sizePx * 0.36)} className="tile-map-resource-icon" />
          ))}
        </span>
      ) : null}
    </div>
  );
});

export default Tile;
