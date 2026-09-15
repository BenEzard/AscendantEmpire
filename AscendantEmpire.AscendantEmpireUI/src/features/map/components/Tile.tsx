// src/features/map-gallery/components/Tile.tsx
import React from "react";
import type { Geography } from "@/types/map/Geography";
import type { TileCell } from "./TileTypes";
import { iconMap } from "./mapEditorIcons";
import { cssColorToRgba, pickTextColorOnBg } from "@/util/color";
import ObservedStructure from "./ObservedStructure";
import ObservedForce from "./ObservedForce";
import KnownForceMarker from "./KnownForceMarker";
import KnownStructureMarker from "./KnownStructureMarker";
import type { KnownForce, KnownStructure } from "@/features/intelligence/api/intelligenceApi";

export interface TileProps {
  tile: TileCell;
  sizePx: number;
  geography: Geography;
  /**
   * Whether the force this tile draws is part of the selection. A boolean rather than the selection set itself, so that changing the selection only
   * re-renders the tiles whose drawn force actually went in or out of it — a new set would change for every tile at once.
   */
  isVisibleForceSelected?: boolean;
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
  /** Structures the player last saw change on this tile, freshest first. Drawn the same way as known forces, beneath them, and only while unobserved. */
  knownStructures?: readonly KnownStructure[];
  /** The round being played now, which known-force and known-structure markers fade against. */
  currentRoundNumber?: number | null;
  onClick?: (tile: TileCell) => void;
  onHold?: (tile: TileCell) => void;
  className?: string;
  /**
   * The CSS background to paint the tile with, overriding the faint default. A string rather than a style callback, so the memo compares what the tile
   * looks like instead of which function produced it.
   */
  background?: string;
  /** Draws the highlight outline around the tile, for the tile the vision tool is pointed at. */
  isHighlighted?: boolean;
  /** The hover tooltip. */
  title?: string;
}

const HOLD_DELAY_MS = 350;

/** The outline drawn around a highlighted tile. Inset by its own width so it sits inside the tile rather than under its neighbours. */
const HIGHLIGHT_OUTLINE = "2px solid rgba(255, 255, 0, 0.8)";
const HIGHLIGHT_OUTLINE_OFFSET = "-2px";

/** The background of a tile whose caller supplies none. */
const DEFAULT_BACKGROUND = "rgba(0,0,0,0.03)";

const Tile = React.memo(function Tile({
  tile,
  sizePx,
  geography,
  isVisibleForceSelected = false,
  visibleForceId,
  unitImageSrcByUnitTemplateId,
  isObserved = true,
  knownForces,
  knownStructures,
  currentRoundNumber = null,
  onClick,
  onHold,
  className,
  background,
  isHighlighted = false,
  title,
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

  // A faint default, for a tile whose caller has no background to give it.
  const tileBackground = background ?? DEFAULT_BACKGROUND;

  // Built here from primitive props rather than handed in, so the memo only lets a tile through when something it draws has changed.
  const style: React.CSSProperties = {
    width: sizePx,
    height: sizePx,
    display: "block",
    cursor: clickable ? "pointer" : "default",
    userSelect: "none",

    // no rounding
    borderRadius: 0,

    background: tileBackground,
    border: "1px solid rgba(0,0,0,0.12)",
    outline: isHighlighted ? HIGHLIGHT_OUTLINE : undefined,
    outlineOffset: HIGHLIGHT_OUTLINE_OFFSET,
  };

  const iconKey = geography?.mapEditorIcon ?? null;
  const Icon = iconKey ? iconMap[iconKey] : undefined;

  // Terrain survives losing sight of a tile; the things standing on it do not. The server stops reporting structures and forces
  // once nothing is watching, and the tiles cache keeps whatever was last seen there rather than blanking it — so without this
  // guard an enemy force would stay painted on ground the player can no longer see, long after it had marched off.
  const visibleStructure = isObserved ? (tile.structures?.find((structure) => structure.structureId === 1) ?? null) : null;
  const visibleForce = isObserved ? (tile.forces?.find((force) => force.id === visibleForceId) ?? tile.forces?.[0] ?? null) : null;
  const visibleForceCount = isObserved ? (tile.forces?.length ?? 0) : 0;

  // The other side of the same guard: a memory of a force or a structure is only drawn where there is no live sight to contradict it.
  const knownForcesToDraw = !isObserved && knownForces != null && knownForces.length > 0 ? knownForces : null;
  const knownStructuresToDraw = !isObserved && knownStructures != null && knownStructures.length > 0 ? knownStructures : null;

  const combinedClassName = ["tile", className].filter(Boolean).join(" ");

  const rgba = cssColorToRgba(tileBackground);
  const bgForContrast = rgba && rgba.a < 0.2 ? "#ffffff" : tileBackground;
  const iconColor = pickTextColorOnBg(bgForContrast);

  return (
    <div
      className={combinedClassName}
      role={clickable ? "button" : undefined}
      title={title}
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
          isSelected={isVisibleForceSelected}
          unitImageSrcByUnitTemplateId={unitImageSrcByUnitTemplateId}
        />
      ) : null}
      {/* Before the force marker, so a force remembered on a tile is drawn over the structure remembered there. */}
      {knownStructuresToDraw ? <KnownStructureMarker knownStructures={knownStructuresToDraw} currentRoundNumber={currentRoundNumber} /> : null}
      {knownForcesToDraw ? (
        <KnownForceMarker knownForces={knownForcesToDraw} currentRoundNumber={currentRoundNumber} unitImageSrcByUnitTemplateId={unitImageSrcByUnitTemplateId} />
      ) : null}
    </div>
  );
});

export default Tile;
