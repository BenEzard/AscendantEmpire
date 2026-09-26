// src/features/map-gallery/components/TileGrid.tsx
import React from "react";
import Tile, { type TileMapResourceMarker } from "./Tile";
import RiverOverlay from "./RiverOverlay";
import type { TileCell } from "../types/TileTypes";
import type { Geography } from "@/types/map/Geography";
import { isSameEdge, resolveEdgeFromPoint, type TileEdgeRef } from "../tools/riverEdgePicking";

export const TILE_SIZE_PX = 50;

// Static styles hoisted to module scope so they aren't re-allocated on every render.
const CONTAINER_STYLE: React.CSSProperties = { overflow: "auto", border: "1px solid rgba(0,0,0,0.125)" };

// The grid and everything drawn over it share one positioned box, so an overlay's (0, 0) is the grid's own top-left corner and the
// two stay in step however the container is scrolled.
const OVERLAY_LAYER_STYLE: React.CSSProperties = { position: "relative", width: "fit-content" };

// The surface that turns a pointer position into a river edge. Only mounted while the River tool is on, so nothing else in the
// editor has to know it exists, and no other tool has its clicks intercepted.
const EDGE_PICKER_STYLE: React.CSSProperties = { position: "absolute", inset: 0, cursor: "crosshair" };

export interface TileGridProps {
  width: number;
  height: number;
  tiles2d: TileCell[][];
  geographies: Geography[];
  onTileClick?: (tile: TileCell) => void;
  onTileHover?: (tile: TileCell) => void;
  onTileHoverEnd?: () => void;

  /**
   * Optional hooks you can use for tooltips / styling.
   */
  getTileTitle?: (tile: TileCell) => string;
  getTileStyle?: (tile: TileCell) => React.CSSProperties;
  tileClassName?: string;

  /** Set of "x,y" keys for tiles that should display a start-position marker. */
  startPositionKeys?: ReadonlySet<string>;

  /** MapResources to draw, by "x,y" key. The arrays must keep their identity between renders, or every tile re-renders. */
  mapResourceMarkers?: ReadonlyMap<string, TileMapResourceMarker[]>;

  /**
   * Turns on river-edge picking: a transparent surface over the grid that resolves the pointer to the nearest tile edge.
   *
   * Supplying either handler mounts it, and while it is mounted the tiles underneath receive no clicks — which is what the River
   * tool wants, since a click on a tile means an edge of that tile and not the tile itself.
   */
  onRiverEdgeClick?: (edge: TileEdgeRef) => void;
  onRiverEdgeHover?: (edge: TileEdgeRef | null) => void;

  /** The edge to mark as the one a click would act on. */
  hoveredRiverEdge?: TileEdgeRef | null;

  /** Whether a click on that edge would erase the river rather than draw one, which the marker shows in its own colour. */
  hoveredRiverEdgeErases?: boolean;
}

const TileGrid = React.memo(function TileGrid({
  width,
  height,
  tiles2d,
  geographies,
  onTileClick,
  onTileHover,
  onTileHoverEnd,
  getTileTitle,
  getTileStyle,
  tileClassName,
  startPositionKeys,
  mapResourceMarkers,
  onRiverEdgeClick,
  onRiverEdgeHover,
  hoveredRiverEdge,
  hoveredRiverEdgeErases,
}: TileGridProps) {
  // Flatten row-major (y then x)
  const flat = React.useMemo(() => tiles2d.flat(), [tiles2d]);

  // Index geographies by their type id once per geographies change, so the per-tile
  // lookup below is an O(1) Map.get() instead of an O(geographies) Array.find() that
  // would otherwise run for every tile on every render (O(tiles × geographies)).
  const geographyByType = React.useMemo(() => {
    const m = new Map<number, Geography>();
    for (const g of geographies) {
      m.set(g.campaignMapGeographyType, g);
    }
    return m;
  }, [geographies]);

  // Grid template depends on width; memoize so the style object identity is stable
  // across renders where width hasn't changed.
  const gridStyle = React.useMemo<React.CSSProperties>(
    () => ({
      display: "grid",
      gridTemplateColumns: `repeat(${width}, ${TILE_SIZE_PX}px)`,
      gridAutoRows: `${TILE_SIZE_PX}px`,
      gap: 0, // crisp grid (no spacing)
    }),
    [width],
  );

  const edgePickingOn = onRiverEdgeClick != null || onRiverEdgeHover != null;

  // The pointer's position within the picking surface, which is the same box the grid occupies. offsetX/offsetY would be relative
  // to whatever child the pointer is actually over, so the surface's own bounding box is measured instead.
  const resolveEdgeFromEvent = React.useCallback(
    (event: React.MouseEvent<HTMLDivElement>): TileEdgeRef | null => {
      const bounds = event.currentTarget.getBoundingClientRect();

      return resolveEdgeFromPoint(event.clientX - bounds.left, event.clientY - bounds.top, TILE_SIZE_PX, width, height);
    },
    [width, height],
  );

  const handleEdgeMove = React.useCallback(
    (event: React.MouseEvent<HTMLDivElement>) => {
      const edge = resolveEdgeFromEvent(event);

      // Only reported when it has actually changed, so a pointer moving within one half of one tile does not re-render the overlay
      // on every mousemove event.
      if (!isSameEdge(edge, hoveredRiverEdge ?? null)) {
        onRiverEdgeHover?.(edge);
      }
    },
    [resolveEdgeFromEvent, hoveredRiverEdge, onRiverEdgeHover],
  );

  const handleEdgeClick = React.useCallback(
    (event: React.MouseEvent<HTMLDivElement>) => {
      const edge = resolveEdgeFromEvent(event);

      if (edge != null) {
        onRiverEdgeClick?.(edge);
      }
    },
    [resolveEdgeFromEvent, onRiverEdgeClick],
  );

  return (
    <div style={CONTAINER_STYLE} onMouseLeave={onTileHoverEnd}>
      <div style={OVERLAY_LAYER_STYLE}>
        <div style={gridStyle}>
          {flat.map((t) => {
            const g = t.geographyTypeId == null ? null : (geographyByType.get(t.geographyTypeId) ?? null);
            if (t.geographyTypeId != null && !g) {
              throw new Error(`No Geography found for geographyTypeId=${t.geographyTypeId} at (${t.x},${t.y})`);
            }

            return (
              <Tile
                key={`${t.x},${t.y}`}
                tile={t}
                geography={g}
                sizePx={TILE_SIZE_PX}
                onClick={onTileClick}
                onHover={onTileHover}
                getTitle={getTileTitle}
                getStyle={getTileStyle}
                className={tileClassName}
                showStartPosition={startPositionKeys?.has(`${t.x},${t.y}`) ?? false}
                mapResources={mapResourceMarkers?.get(`${t.x},${t.y}`)}
              />
            );
          })}
        </div>

        {/* Over the tiles, because a river runs along the boundary between two of them and belongs to neither, so it cannot be
            drawn inside either one. */}
        <RiverOverlay
          tiles2d={tiles2d}
          width={width}
          height={height}
          tileSizePx={TILE_SIZE_PX}
          hoveredEdge={hoveredRiverEdge}
          hoveredEdgeErases={hoveredRiverEdgeErases}
        />

        {edgePickingOn && (
          <div
            style={EDGE_PICKER_STYLE}
            onMouseMove={handleEdgeMove}
            onMouseLeave={() => onRiverEdgeHover?.(null)}
            onClick={handleEdgeClick}
            data-testid="river-edge-picker"
          />
        )}
      </div>
    </div>
  );
});

export default TileGrid;
