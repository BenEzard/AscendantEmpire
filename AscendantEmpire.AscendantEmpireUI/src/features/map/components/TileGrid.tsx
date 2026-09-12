import React from "react";
import type { Geography } from "@/types/map/Geography";
import type { TileCell } from "./TileTypes";
import Tile from "./Tile";
import type { KnownForce, KnownStructure } from "@/features/intelligence/api/intelligenceApi";

export const TILE_SIZE_PX = 50;

// Static container style hoisted to module scope so it isn't re-allocated each render.
const CONTAINER_STYLE: React.CSSProperties = { overflow: "auto", border: "1px solid rgba(0,0,0,0.125)" };

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
   * The forces the player last saw on a tile, freshest first, for last-known-position markers. Should return the same array for an unchanged tile, or every
   * memoised tile re-renders. Omitted means no markers at all.
   */
  getKnownForces?: (tile: TileCell) => readonly KnownForce[] | undefined;
  /** The structures the player last saw change on a tile, freshest first. The same stability rule as `getKnownForces` applies. */
  getKnownStructures?: (tile: TileCell) => readonly KnownStructure[] | undefined;
  /** The round being played now, which markers fade against. */
  currentRoundNumber?: number | null;
  onTileClick?: (tile: TileCell) => void;
  onTileHold?: (tile: TileCell) => void;

  /**
   * Optional hooks you can use for tooltips / styling.
   */
  getTileTitle?: (tile: TileCell) => string;
  getTileStyle?: (tile: TileCell) => React.CSSProperties;
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
  getKnownForces,
  getKnownStructures,
  currentRoundNumber,
  onTileClick,
  onTileHold,
  getTileTitle,
  getTileStyle,
  tileClassName,
}: TileGridProps) {
  void height;
  // Flatten row-major (y then x)
  const flat = React.useMemo(() => tiles2d.flat(), [tiles2d]);

  // Index geographies by their type id once per geographies change, so the per-tile
  // lookup is an O(1) Map.get() instead of an O(geographies) Array.find() running for
  // every tile on every render (O(tiles Ã— geographies)).
  const geographyByType = React.useMemo(() => {
    const m = new Map<number, Geography>();
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
      <div style={gridStyle}>
        {flat.map((t) => {
          const g = t.geographyTypeId == null ? undefined : geographyByType.get(t.geographyTypeId);
          if (!g) {
            throw new Error(`No Geography found for geographyTypeId=${t.geographyTypeId} at (${t.x},${t.y})`);
          }

          return (
            <Tile
              key={`${t.x},${t.y}`}
              tile={t}
              geography={g}
              sizePx={TILE_SIZE_PX}
              selectedForceIds={selectedForceIds}
              unitImageSrcByUnitTemplateId={unitImageSrcByUnitTemplateId}
              visibleForceId={getVisibleForceId?.(t)}
              isObserved={isTileObserved?.(t) ?? true}
              knownForces={getKnownForces?.(t)}
              knownStructures={getKnownStructures?.(t)}
              currentRoundNumber={currentRoundNumber}
              onClick={onTileClick}
              onHold={onTileHold}
              getTitle={getTileTitle}
              getStyle={getTileStyle}
              className={tileClassName}
            />
          );
        })}
      </div>
    </div>
  );
});

export default TileGrid;
