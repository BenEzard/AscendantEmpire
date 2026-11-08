// src/features/map-gallery/components/Tile.tsx
import React from "react";
import type { Geography } from "@/types/map/Geography";
import type { TileCell, TileMapResourceMarker } from "./TileTypes";
import { iconMap } from "./mapEditorIcons";
import MapResourceIcon from "@/components/map/MapResourceIcon";
import { describeMapResources } from "@/features/map/util/mapResourceMarkers";
import { cssColorToRgba, pickTextColorOnBg } from "@/util/color";
import ObservedStructure from "./ObservedStructure";
import ObservedForce from "./ObservedForce";
import KnownForceMarker from "./KnownForceMarker";
import KnownStructureMarker from "./KnownStructureMarker";
import { structureImageSrc } from "@/features/map/util/structureImages";
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
  /**
   * The catalogue code of each kind of structure, from /structures/display.
   *
   * Only the known-structure marker needs it: a live structure arrives from the map read with its own code, but a remembered
   * one carries an id alone and the artwork is keyed on the code.
   */
  structureCodeByStructureId?: ReadonlyMap<number, string>;
  /**
   * The MapResources the player has discovered on this tile, in slot order. Drawn in the tile's bottom-right corner, exactly as the map editor's tile draws
   * its placements, so a berry patch looks the same in both.
   *
   * Not gated on sight, unlike the forces and structures above: what the player found is a fact about the map they have learned, and a patch does not stop
   * existing because they marched away from it. Only its quantity waits on an observer, and that absence is already in the marker.
   *
   * Should keep one identity for an unchanged tile, like `knownForces`, or every memoised tile re-renders.
   */
  mapResources?: readonly TileMapResourceMarker[];
  onClick?: (tile: TileCell) => void;
  onHold?: (tile: TileCell) => void;
  /** Called with the tile when the pointer enters it. Should keep one identity, like `onClick`, or every tile re-renders when it changes. */
  onHover?: (tile: TileCell) => void;
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
  structureCodeByStructureId,
  mapResources,
  onClick,
  onHold,
  onHover,
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
  //
  // The first structure the client has art for, rather than a hardcoded kind. This used to look for `structureId === 1` — the
  // one catalogue row anyone had, inserted by hand — which drew nothing at all for any other kind. ObservedStructure draws
  // nothing for a code it has no sprite for, so picking one that it can draw is what keeps a tile from going blank when it
  // holds, say, a totem pole as well as a village.
  const visibleStructure = isObserved
    ? (tile.structures?.find((structure) => structureImageSrc(structure.structureCode) != null) ?? null)
    : null;
  const visibleForce = isObserved ? (tile.forces?.find((force) => force.id === visibleForceId) ?? tile.forces?.[0] ?? null) : null;
  const visibleForceCount = isObserved ? (tile.forces?.length ?? 0) : 0;

  // The other side of the same guard: a memory of a force or a structure is only drawn where there is no live sight to contradict it.
  const knownForcesToDraw = !isObserved && knownForces != null && knownForces.length > 0 ? knownForces : null;
  const knownStructuresToDraw = !isObserved && knownStructures != null && knownStructures.length > 0 ? knownStructures : null;

  const combinedClassName = ["tile", className].filter(Boolean).join(" ");

  const rgba = cssColorToRgba(tileBackground);
  const bgForContrast = rgba && rgba.a < 0.2 ? "#ffffff" : tileBackground;
  const iconColor = pickTextColorOnBg(bgForContrast);

  // Ground the player has never been to says nothing about itself: it is drawn as solid fog over a placeholder biome, so a resource on it would be the one
  // thing showing through. The server withholds them as well; this keeps the fog honest whatever a later discovery rule reports.
  const knownMapResources = tile.isExplored ? mapResources : undefined;

  // Only a resource with an icon of its own is drawn. A type the biome already says — a forest's wood — has none, and MapResourceIcon's fish stand-in is
  // for the map editor, so those are left off the tile and named in its title instead.
  const drawnMapResources = knownMapResources?.filter((marker) => marker.iconPath != null);

  // The title names the tile's MapResources after whatever the caller's title says, the same way the map editor's tile does, and with each estimate where
  // there is one to give.
  const mapResourcesTitle = knownMapResources != null && knownMapResources.length > 0 ? `MapResources: ${describeMapResources(knownMapResources)}` : null;
  const titleWithMapResources = [title, mapResourcesTitle].filter(Boolean).join("\n") || undefined;

  return (
    <div
      className={combinedClassName}
      role={clickable ? "button" : undefined}
      title={titleWithMapResources}
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
      onPointerEnter={() => {
        // Only entering is reported. Leaving one tile is always entering another, or leaving the map, which the grid reports itself — so reporting leaves
        // too would briefly say the pointer was nowhere between every pair of tiles.
        onHover?.(tile);
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
      {/* Before the structure and the force, so anything standing on the tile is drawn over its resources rather than hidden behind them. */}
      {drawnMapResources != null && drawnMapResources.length > 0 ? (
        <span className="tile-map-resources" aria-hidden>
          {drawnMapResources.map((marker) => (
            <MapResourceIcon key={marker.slot} iconPath={marker.iconPath} size={Math.round(sizePx * 0.36)} />
          ))}
        </span>
      ) : null}
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
      {knownStructuresToDraw ? (
        <KnownStructureMarker
          knownStructures={knownStructuresToDraw}
          currentRoundNumber={currentRoundNumber}
          structureCodeByStructureId={structureCodeByStructureId}
        />
      ) : null}
      {knownForcesToDraw ? (
        <KnownForceMarker knownForces={knownForcesToDraw} currentRoundNumber={currentRoundNumber} unitImageSrcByUnitTemplateId={unitImageSrcByUnitTemplateId} />
      ) : null}
    </div>
  );
});

export default Tile;
