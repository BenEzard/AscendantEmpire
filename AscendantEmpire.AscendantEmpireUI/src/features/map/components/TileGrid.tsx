import React from "react";
import type { Geography } from "@/types/map/Geography";
import type { TileCell, TileMapResourceMarker } from "./TileTypes";
import Tile from "./Tile";
import RiverOverlay from "./RiverOverlay";
import OwnershipOverlay from "./OwnershipOverlay";
import SettlementLabelOverlay from "./SettlementLabelOverlay";
import type { KnownForce, KnownStructure } from "@/features/intelligence/api/intelligenceApi";
import type { CampaignMapGeographyType } from "@/types/map/Geography";

export const TILE_SIZE_PX = 50;

// Static container style hoisted to module scope so it isn't re-allocated each render.
const CONTAINER_STYLE: React.CSSProperties = { overflow: "auto", border: "1px solid rgba(0,0,0,0.125)" };

// The positioning context for overlays drawn over the tiles. Inside the scrolling container, so an overlay placed by tile coordinates scrolls with the map.
const OVERLAY_LAYER_STYLE: React.CSSProperties = { position: "relative" };

export interface TileGridProps {
  width: number;
  height: number;
  tiles2d: TileCell[][];
  geographies: Geography[];
  containerRef?: React.Ref<HTMLDivElement>;
  /** Every force currently selected. A set rather than a single id since selection is a multi-select. */
  selectedForceIds?: ReadonlySet<number>;
  unitImageSrcByUnitTemplateId?: ReadonlyMap<number, string>;
  getVisibleForceId?: (tile: TileCell) => number | undefined;
  /**
   * Whether one of the player's observers can see the tile right now, which decides whether its structures and forces are drawn
   * at all. Omitted means every tile is observed, which is what a grid with no fog of war wants.
   */
  isTileObserved?: (tile: TileCell) => boolean;
  /**
   * Each player's faction colour, which territory borders are drawn in. Omitted means no borders at all, which is what a grid
   * with no ownership on it — the map editor's — wants.
   */
  factionColourByPlayerId?: ReadonlyMap<number, string>;
  /**
   * The forces the player last saw on a tile, freshest first, for last-known-position markers. Should return the same array for an unchanged tile, or every
   * memoised tile re-renders. Omitted means no markers at all.
   */
  getKnownForces?: (tile: TileCell) => readonly KnownForce[] | undefined;
  /** The structures the player last saw change on a tile, freshest first. The same stability rule as `getKnownForces` applies. */
  getKnownStructures?: (tile: TileCell) => readonly KnownStructure[] | undefined;
  /**
   * The MapResources the player has discovered on a tile, in slot order. The same stability rule as `getKnownForces` applies. Omitted means none at all,
   * which is what a grid with no discoveries behind it — the map editor's — wants.
   */
  getMapResources?: (tile: TileCell) => readonly TileMapResourceMarker[] | undefined;
  /** The round being played now, which markers fade against. */
  currentRoundNumber?: number | null;
  /**
   * The catalogue code of each kind of structure, from /structures/display. Passed straight to each tile.
   *
   * Only the known-structure marker needs it: a live structure arrives with its own code, but a remembered one carries an id
   * alone and the artwork is keyed on the code.
   */
  structureCodeByStructureId?: ReadonlyMap<number, string>;
  /** Called with the clicked tile. Its identity may change freely: the grid hands every tile one stable handler that forwards to the latest one. */
  onTileClick?: (tile: TileCell) => void;
  /**
   * Called with the double-clicked tile. The same stability guarantee as `onTileClick` applies, and both clicks still reach
   * `onTileClick` first — a double click opens what the tile holds rather than replacing what a click does.
   */
  onTileDoubleClick?: (tile: TileCell) => void;
  /** Called when a tile is held. The same stability guarantee as `onTileClick` applies. */
  onTileHold?: (tile: TileCell) => void;
  /**
   * Called with the tile the pointer enters, and with null when the pointer leaves the map. The same stability guarantee as `onTileClick` applies. Moving
   * between tiles reports only the tile entered, never a null in between.
   */
  onTileHover?: (tile: TileCell | null) => void;
  /**
   * Drawn over the tiles, positioned against the top-left of the grid (tile (x, y) starts at `x * TILE_SIZE_PX`, `y * TILE_SIZE_PX`), so it scrolls with the
   * map. Keep its identity stable: the grid is memoised, and a fresh element on every parent render would re-render the grid with it.
   */
  children?: React.ReactNode;

  /*
   * Per-tile appearance. Each of these is called by the grid for every tile, and what it returns is handed to the tile as a plain value. That is what lets
   * the memoised tiles skip re-rendering when a callback takes a new identity — only a tile whose returned value actually changed re-renders. They must
   * therefore return primitives: a fresh object per call would defeat the memo just as a fresh callback used to.
   */

  /** The hover tooltip. */
  getTileTitle?: (tile: TileCell) => string;
  /** The CSS background to paint the tile with. Omitted means a faint default. */
  getTileBackground?: (tile: TileCell) => string;
  /** Whether to draw the highlight outline around the tile. */
  isTileHighlighted?: (tile: TileCell) => boolean;
  tileClassName?: string;
}

const TileGrid = React.memo(function TileGrid({
  width,
  height,
  tiles2d,
  geographies,
  containerRef,
  selectedForceIds,
  unitImageSrcByUnitTemplateId,
  getVisibleForceId,
  isTileObserved,
  factionColourByPlayerId,
  getKnownForces,
  getKnownStructures,
  getMapResources,
  currentRoundNumber,
  structureCodeByStructureId,
  onTileClick,
  onTileDoubleClick,
  onTileHold,
  onTileHover,
  children,
  getTileTitle,
  getTileBackground,
  isTileHighlighted,
  tileClassName,
}: TileGridProps) {
  // The latest click and hold handlers, held in refs so the tiles can be given handlers that never change identity. A caller's handler typically depends
  // on the selection, so passing it straight through would re-render every tile on the map each time the selection changed.
  const onTileClickRef = React.useRef(onTileClick);
  onTileClickRef.current = onTileClick;

  const onTileDoubleClickRef = React.useRef(onTileDoubleClick);
  onTileDoubleClickRef.current = onTileDoubleClick;

  const onTileHoldRef = React.useRef(onTileHold);
  onTileHoldRef.current = onTileHold;

  const handleTileClick = React.useCallback((tile: TileCell) => onTileClickRef.current?.(tile), []);
  const handleTileDoubleClick = React.useCallback((tile: TileCell) => onTileDoubleClickRef.current?.(tile), []);
  const handleTileHold = React.useCallback((tile: TileCell) => onTileHoldRef.current?.(tile), []);

  const onTileHoverRef = React.useRef(onTileHover);
  onTileHoverRef.current = onTileHover;

  const handleTileHover = React.useCallback((tile: TileCell) => onTileHoverRef.current?.(tile), []);
  const handleGridLeave = React.useCallback(() => onTileHoverRef.current?.(null), []);

  // Whether a handler is supplied at all still reaches the tiles, since it changes how they behave: a tile with no click handler isn't drawn as a button,
  // and one with no hold handler never starts the hold timer. That only changes when a handler appears or disappears, not when it is replaced.
  const tileClickHandler = onTileClick ? handleTileClick : undefined;
  const tileDoubleClickHandler = onTileDoubleClick ? handleTileDoubleClick : undefined;
  const tileHoldHandler = onTileHold ? handleTileHold : undefined;
  const tileHoverHandler = onTileHover ? handleTileHover : undefined;

  // The border overlay asks per tile whether it is watched, so it needs the predicate whether or not the caller supplied one. A
  // grid with no fog — the editor's — treats every tile as observed, exactly as the tiles themselves do.
  const isTileObservedOrAll = React.useMemo(() => isTileObserved ?? (() => true), [isTileObserved]);
  // Flatten row-major (y then x)
  const flat = React.useMemo(() => tiles2d.flat(), [tiles2d]);

  // Index geographies by their type id once per geographies change, so the per-tile
  // lookup is an O(1) Map.get() instead of an O(geographies) Array.find() running for
  // every tile on every render (O(tiles Ã— geographies)).
  const geographyByType = React.useMemo(() => {
    const m = new Map<CampaignMapGeographyType, Geography>();
    for (const g of geographies) {
      m.set(g.campaignMapGeographyType, g);
    }
    return m;
  }, [geographies]);

  // Grid template depends on width; memoize so the style object identity stays stable.
  const gridStyle = React.useMemo<React.CSSProperties>(
    () => ({
      display: "grid",
      gridTemplateColumns: `repeat(${width}, ${TILE_SIZE_PX}px)`,
      gridAutoRows: `${TILE_SIZE_PX}px`,
      gap: 0, // crisp grid (no spacing)
    }),
    [width],
  );

  return (
    <div ref={containerRef} style={CONTAINER_STYLE}>
      {/* Leaving this layer is leaving the map. Tiles only report the tile entered, so this is the one place the pointer is known to be off every tile. */}
      <div style={OVERLAY_LAYER_STYLE} onPointerLeave={handleGridLeave}>
        <div style={gridStyle}>
          {flat.map((t) => {
            const g = t.geographyTypeId == null ? undefined : geographyByType.get(t.geographyTypeId);
            if (!g) {
              throw new Error(`No Geography found for geographyTypeId=${t.geographyTypeId} at (${t.x},${t.y})`);
            }

            // The force the tile draws, falling back to its first force the same way Tile does, so the selected state below describes the force that is
            // actually on screen.
            const visibleForceId = getVisibleForceId?.(t) ?? t.forces?.[0]?.id;

            // Resolved to a boolean here rather than passing the selection set down. A new set reaches every tile; a boolean only differs for the tiles whose
            // drawn force went into or out of the selection.
            const isVisibleForceSelected = visibleForceId != null && (selectedForceIds?.has(visibleForceId) ?? false);

            return (
              <Tile
                key={`${t.x},${t.y}`}
                tile={t}
                geography={g}
                sizePx={TILE_SIZE_PX}
                isVisibleForceSelected={isVisibleForceSelected}
                unitImageSrcByUnitTemplateId={unitImageSrcByUnitTemplateId}
                visibleForceId={visibleForceId}
                isObserved={isTileObserved?.(t) ?? true}
                knownForces={getKnownForces?.(t)}
                knownStructures={getKnownStructures?.(t)}
                mapResources={getMapResources?.(t)}
                currentRoundNumber={currentRoundNumber}
                structureCodeByStructureId={structureCodeByStructureId}
                onClick={tileClickHandler}
                onDoubleClick={tileDoubleClickHandler}
                onHold={tileHoldHandler}
                onHover={tileHoverHandler}
                title={getTileTitle?.(t)}
                background={getTileBackground?.(t)}
                isHighlighted={isTileHighlighted?.(t) ?? false}
                className={tileClassName}
              />
            );
          })}
        </div>
        {/* Over the tiles, because a river runs along the boundary between two of them and belongs to neither — so it cannot be
            drawn inside either one. Before `children`, so anything a caller overlays sits above it. */}
        <RiverOverlay tiles2d={tiles2d} width={width} height={height} tileSizePx={TILE_SIZE_PX} />
        {/* Over the rivers, because a border following a river has to stay readable where the two run along the same edge, and
            under `children` so anything a caller overlays still sits above both. Drawn only where faction colours are in hand:
            without them there is nothing to colour a territory with. */}
        {factionColourByPlayerId != null && (
          <OwnershipOverlay
            tiles2d={tiles2d}
            width={width}
            height={height}
            tileSizePx={TILE_SIZE_PX}
            factionColourByPlayerId={factionColourByPlayerId}
            isTileObserved={isTileObservedOrAll}
          />
        )}
        {/* Last of the map's own layers, so a settlement's name is never hidden behind a border, a river or a unit. Still under
            `children`, which is a caller's to place as it likes. */}
        <SettlementLabelOverlay tiles2d={tiles2d} width={width} height={height} tileSizePx={TILE_SIZE_PX} />
        {children}
      </div>
    </div>
  );
});

export default TileGrid;
