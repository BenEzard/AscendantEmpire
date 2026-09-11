// src/features/map-gallery/components/Tile.tsx
import React from "react";
import type { Geography } from "@/types/map/Geography";
import type { TileCell } from "./TileTypes";
import { iconMap } from "./mapEditorIcons";
import { cssColorToRgba, pickTextColorOnBg } from "@/util/color";
import ObservedStructure from "./ObservedStructure";
import ObservedForce from "./ObservedForce";
import KnownForceMarker from "./KnownForceMarker";
import type { KnownForce } from "@/features/intelligence/api/intelligenceApi";

export interface TileProps {
  tile: TileCell;
  sizePx: number;
  geography: Geography;
  /** Every force currently selected. A set rather than a single id since selection is a multi-select. */
  selectedForceIds?: ReadonlySet<number>;
  visibleForceId?: number;
  unitImageSrcByUnitTemplateId?: ReadonlyMap<number, string>;
  /**
   * Whether one of the player's observers can see this tile right now. Defaults to true, so a grid with no fog of war — the
   * map editor's, say — draws everything as it always did.
   */
  isObserved?: boolean;
  /**
   * Forces the player last saw on this tile, freshest first. Drawn as a fading marker, and only while the tile is not observed — under live sight the map
   * already shows what is really there.
   */
  knownForces?: readonly KnownForce[];
  /** The round being played now, which known-force markers fade against. */
  currentRoundNumber?: number | null;
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
  selectedForceIds,
  visibleForceId,
  unitImageSrcByUnitTemplateId,
  isObserved = true,
  knownForces,
  currentRoundNumber = null,
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

  // Terrain survives losing sight of a tile; the things standing on it do not. The server stops reporting structures and forces
  // once nothing is watching, and the tiles cache keeps whatever was last seen there rather than blanking it — so without this
  // guard an enemy force would stay painted on ground the player can no longer see, long after it had marched off.
  const visibleStructure = isObserved ? (tile.structures?.find((structure) => structure.structureId === 1) ?? null) : null;
  const visibleForce = isObserved ? (tile.forces?.find((force) => force.id === visibleForceId) ?? tile.forces?.[0] ?? null) : null;
  const visibleForceCount = isObserved ? (tile.forces?.length ?? 0) : 0;

  // The other side of the same guard: a memory of a force is only drawn where there is no live sight to contradict it.
  const knownForcesToDraw = !isObserved && knownForces != null && knownForces.length > 0 ? knownForces : null;

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
      onPointerDown={(event) => {
        holdTriggeredRef.current = false;
        clearHoldTimer();

        if (!onHold) return;

        // Primary button only. Touch and pen both report 0 here, so this costs them nothing — it is right- and
        // middle-click that must not start the timer, now that a completed hold issues a move order rather than
        // merely showing a toast.
        if (event.button !== 0) return;

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
      {visibleForce ? (
        <ObservedForce
          force={visibleForce}
          unitInstanceCount={visibleForceCount}
          isSelected={selectedForceIds?.has(visibleForce.id) ?? false}
          unitImageSrcByUnitTemplateId={unitImageSrcByUnitTemplateId}
        />
      ) : null}
      {knownForcesToDraw ? (
        <KnownForceMarker knownForces={knownForcesToDraw} currentRoundNumber={currentRoundNumber} unitImageSrcByUnitTemplateId={unitImageSrcByUnitTemplateId} />
      ) : null}
    </div>
  );
});

export default Tile;
