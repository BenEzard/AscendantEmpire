// src/features/map-gallery/components/Tile.tsx
import React from "react";
import type { Geography } from "@/types/map/Geography";
import type { TileCell } from "./TileTypes";
import { iconMap } from "./mapEditorIcons";
import { cssColorToRgba, pickTextColorOnBg } from "@/util/color";
import ObservedStructure from "./ObservedStructure";

import primitiveManWithClubImage from "@/assets/units/primitive-man-with-club_40px.png";
import hunterWithDogImage from "@/assets/units/hunter-with-dog_40px.png";

const troopIconByUnitDesignId: Record<number, string> = {
  1: primitiveManWithClubImage,
  2: hunterWithDogImage,
};

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
  const visibleStructure = tile.structures?.find((structure) => structure.structureId === 1) ?? null;
  const visibleTroop = tile.troops?.find((troop) => troopIconByUnitDesignId[troop.unitDesignId] != null) ?? null;
  const troopImageSrc = visibleTroop ? troopIconByUnitDesignId[visibleTroop.unitDesignId] : undefined;

  const style = { ...baseStyle, ...(getStyle?.(tile) ?? {}) };
  const combinedClassName = ["tile", className].filter(Boolean).join(" ");

  const bgCandidate =
    (typeof style.background === "string" ? style.background : undefined) ??
    (typeof style.backgroundColor === "string" ? style.backgroundColor : undefined);
  const rgba = cssColorToRgba(bgCandidate);
  const bgForContrast = rgba && rgba.a < 0.2 ? "#ffffff" : bgCandidate;
  const iconColor = pickTextColorOnBg(bgForContrast);

  return (
    <div className={combinedClassName} role={clickable ? "button" : undefined} title={getTitle?.(tile)} onClick={() => onClick?.(tile)} style={style}>
      {Icon ? <Icon className="tile-icon-fill" aria-hidden style={{ color: iconColor }} /> : null}
      {visibleStructure ? <ObservedStructure structure={visibleStructure} /> : null}
      {troopImageSrc ? <img className="observed-troop__image" alt="" aria-hidden src={troopImageSrc} /> : null}
    </div>
  );
});

export default Tile;
