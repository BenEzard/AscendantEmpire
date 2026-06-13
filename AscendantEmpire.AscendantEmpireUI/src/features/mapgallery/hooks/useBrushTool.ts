import { useCallback, useEffect, useMemo, useState } from "react";
import type { TileCell } from "@/features/mapgallery/types/TileTypes";
import { buildBrushCoordinates, clampBrushRadius, type BrushKind } from "@/features/mapgallery/tools/brush";
import type { ActiveTool } from "@/features/mapgallery/types/MapGalleryEditorTypes";

interface UseBrushToolOptions {
  /** Map dimensions used to clamp brush radii; null when no map is open. */
  mapDims: { width: number; height: number } | null;
  activeTool: ActiveTool;
  hasOpenMap: boolean;
}

/**
 * Owns paint-brush state (kind, per-kind radii, hovered tile) and derives the brush
 * coordinate set and hover preview. Keeps stored radii valid for the current map and
 * clears the hover when paint is not the active tool.
 */
export function useBrushTool(opts: UseBrushToolOptions) {
  const { mapDims, activeTool, hasOpenMap } = opts;

  const [brushKind, setBrushKind] = useState<BrushKind>("square");
  const [brushRadii, setBrushRadii] = useState<Record<BrushKind, number>>({
    square: 0,
    diamond: 1,
    vline: 1,
    hline: 1,
    diagDown: 1,
    diagUp: 1,
  });
  const [hoveredTile, setHoveredTile] = useState<TileCell | null>(null);

  const clampRadius = useCallback((kind: BrushKind, value: number) => clampBrushRadius(kind, value, mapDims), [mapDims]);

  // If the user opens a smaller map, ensure stored radii are still valid.
  useEffect(() => {
    setBrushRadii((prev) => {
      const next: Record<BrushKind, number> = { ...prev };
      (Object.keys(next) as BrushKind[]).forEach((k) => {
        next[k] = clampRadius(k, next[k] ?? 0);
      });
      return next;
    });
  }, [clampRadius]);

  // Hover preview only makes sense while painting.
  useEffect(() => {
    if (activeTool !== "paint") setHoveredTile(null);
  }, [activeTool]);

  const buildBrushCoords = useCallback(
    (origin: TileCell) =>
      buildBrushCoordinates({
        origin,
        kind: brushKind,
        radius: brushRadii[brushKind] ?? 0,
        dims: mapDims,
      }),
    [brushKind, brushRadii, mapDims],
  );

  const previewKeySet = useMemo(() => {
    if (activeTool !== "paint") return null;
    if (!hasOpenMap) return null;
    if (!hoveredTile) return null;

    const coords = buildBrushCoords(hoveredTile);
    return new Set(coords.map((c) => `${c.x},${c.y}`));
  }, [activeTool, hasOpenMap, hoveredTile, buildBrushCoords]);

  // Stable hover handlers so TileGrid's props keep a constant identity while painting.
  const handleTileHover = useCallback((t: TileCell) => setHoveredTile(t), []);
  const handleTileHoverEnd = useCallback(() => setHoveredTile(null), []);

  return {
    brushKind,
    setBrushKind,
    brushRadii,
    setBrushRadii,
    hoveredTile,
    clampRadius,
    buildBrushCoords,
    previewKeySet,
    handleTileHover,
    handleTileHoverEnd,
  };
}
