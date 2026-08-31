// src/features/map-gallery/components/Tile.tsx
import React from "react";
import type { Geography } from "@/types/map/Geography";
import type { TileCell } from "./TileTypes";
import { iconMap } from "./mapEditorIcons";
import { cssColorToRgba, pickTextColorOnBg } from "@/util/color";
import ObservedStructure from "./ObservedStructure";
import ObservedForce from "./ObservedForce";

export interface TileProps {
  tile: TileCell;
  sizePx: number;
  geography: Geography;
  selectedTroopId?: number;
  visibleTroopId?: number;
  troopImageSrcByUnitTemplateId?: ReadonlyMap<number, string>;
  onClick?: (tile: TileCell) => void;
  onHold?: (tile: TileCell) => void;
  className?: string;
  /**
   * Optional styling hooks.
   */
  getTitle?: (tile: TileCell) => string;
  getStyle?: (tile: TileCell) => React.CSSProperties;
}

const HOLD_DELAY_MS = 350;

const Tile = React.memo(function Tile({
  tile,
  sizePx,
  geography,
  selectedTroopId,
  visibleTroopId,
  troopImageSrcByUnitTemplateId,
  onClick,
  onHold,
  className,
  getTitle,
  getStyle,
}: TileProps) {
  const clickable = !!onClick;
  const holdTimerRef = React.useRef<number | null>(null);
  const holdTriggeredRef = React.useRef(false);

  const clearHoldTimer = React.useCallback(() => {
    if (holdTimerRef.current != null) {
      window.clearTimeout(holdTimerRef.current);
      holdTimerRef.current = null;
    }
  }, []);

  React.useEffect(() => () => clearHoldTimer(), [clearHoldTimer]);

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
  const visibleTroop = tile.forces?.find((troop) => troop.id === visibleTroopId) ?? tile.forces?.[0] ?? null;
  const visibleTroopCount = tile.forces?.length ?? 0;

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
      onPointerDown={() => {
        holdTriggeredRef.current = false;
        clearHoldTimer();

        if (!onHold) return;

        holdTimerRef.current = window.setTimeout(() => {
          holdTriggeredRef.current = true;
          holdTimerRef.current = null;
          onHold(tile);
        }, HOLD_DELAY_MS);
      }}
      onPointerUp={() => {
        clearHoldTimer();
      }}
      onPointerLeave={() => {
        clearHoldTimer();
      }}
      onPointerCancel={() => {
        clearHoldTimer();
      }}
      onClick={() => {
        if (holdTriggeredRef.current) {
          holdTriggeredRef.current = false;
          return;
        }

        onClick?.(tile);
      }}
      style={style}>
      {Icon ? <Icon className="tile-icon-fill" aria-hidden style={{ color: iconColor }} /> : null}
      {visibleStructure ? <ObservedStructure structure={visibleStructure} /> : null}
      {visibleTroop ? (
        <ObservedForce
          troop={visibleTroop}
          unitInstanceCount={visibleTroopCount}
          isSelected={visibleTroop.id === selectedTroopId}
          troopImageSrcByUnitTemplateId={troopImageSrcByUnitTemplateId}
        />
      ) : null}
    </div>
  );
});

export default Tile;
