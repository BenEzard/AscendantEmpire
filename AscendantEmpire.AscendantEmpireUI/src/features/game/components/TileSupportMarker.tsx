import { TILE_SIZE_PX } from "@/features/map/components/TileGrid";
import type { TileSupportLevel } from "@/features/map/api/tileSustenanceApi";
import { useHoveredTile, type HoveredTileStore } from "@/features/map/hooks/useHoveredTileStore";
import { SUPPORT_LEVEL_LABELS, tileSupportLevelClass } from "./tileSupportLevelClass";

export interface TileSupportMarkerProps {
  /** Where the pointer is. Read through a subscription, so hovering renders only this marker — never the grid or the page around it. */
  hoveredTileStore: HoveredTileStore;

  /** The support level of each observed tile for the selected forces, keyed by `"x,y"`. Undefined while it loads, which draws nothing. */
  levelByTileKey: ReadonlyMap<string, TileSupportLevel> | undefined;

  /** Tiles in live sight right now, keyed by `"x,y"`. A level is only drawn on one of these, even if a stale answer holds one for a tile since lost from sight. */
  observedTileKeys: ReadonlySet<string>;

  /** Tiles the selected forces already stand on, keyed by `"x,y"`. No marker there: the tile toolbar is already describing that tile. */
  selectedForceTileKeys: ReadonlySet<string>;
}

/**
 * A coloured circle in the centre of the hovered tile, saying whether that tile could feed the selected forces if they moved there — the same colours as the
 * tile toolbar's background.
 *
 * Drawn once, as an overlay inside the tile grid, rather than as a prop on every tile: the pointer crosses tiles constantly, and a per-tile prop would
 * re-render the tiles it left and entered each time. Pointer events pass straight through it, so it never steals a click or a hold from the tile beneath.
 *
 * Mount only while forces are selected. Draws nothing off the map, on an unobserved tile, on the selected forces' own tile, or on a tile with no level.
 */
export default function TileSupportMarker({ hoveredTileStore, levelByTileKey, observedTileKeys, selectedForceTileKeys }: TileSupportMarkerProps) {
  const hoveredTile = useHoveredTile(hoveredTileStore);

  if (hoveredTile == null || levelByTileKey == null) {
    return null;
  }

  const tileKey = `${hoveredTile.x},${hoveredTile.y}`;

  if (!observedTileKeys.has(tileKey) || selectedForceTileKeys.has(tileKey)) {
    return null;
  }

  const supportLevel = levelByTileKey.get(tileKey);

  if (supportLevel == null) {
    return null;
  }

  const className = ["tile-support-marker", tileSupportLevelClass("tile-support-marker", supportLevel)].filter(Boolean).join(" ");

  return (
    <span
      className={className}
      role="img"
      aria-label={SUPPORT_LEVEL_LABELS[supportLevel]}
      // Centred on the tile: the marker's own transform pulls it back by half its size.
      style={{ left: hoveredTile.x * TILE_SIZE_PX + TILE_SIZE_PX / 2, top: hoveredTile.y * TILE_SIZE_PX + TILE_SIZE_PX / 2 }}
    />
  );
}
