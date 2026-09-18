import { TILE_SIZE_PX } from "@/features/map/components/TileGrid";
import type { TileSupport } from "@/features/map/api/tileSupportApi";
import { useHoveredTile, type HoveredTileStore } from "@/features/map/hooks/useHoveredTileStore";
import { tileSupportLevelClass } from "./tileSupportLevelClass";
import { formatSupportTurns, supportTurnsLabel } from "./formatSupportTurns";

export interface TileSupportMarkerProps {
  /** Where the pointer is. Read through a subscription, so hovering renders only this marker — never the grid or the page around it. */
  hoveredTileStore: HoveredTileStore;

  /** The support level and turns of each observed tile for the selected forces, keyed by `"x,y"`. Undefined while it loads, which draws nothing. */
  supportByTileKey: ReadonlyMap<string, TileSupport> | undefined;

  /** Tiles in live sight right now, keyed by `"x,y"`. A level is only drawn on one of these, even if a stale answer holds one for a tile since lost from sight. */
  observedTileKeys: ReadonlySet<string>;

  /** Tiles the selected forces already stand on, keyed by `"x,y"`. No marker there: the tile toolbar is already describing that tile. */
  selectedForceTileKeys: ReadonlySet<string>;
}

/**
 * A coloured circle in the centre of the hovered tile, saying whether that tile could feed the selected forces if they moved there — the same colours as the
 * tile toolbar's background — with the number of turns it could feed them inside, regrowth included (`5+` beyond the forecast).
 *
 * Drawn once, as an overlay inside the tile grid, rather than as a prop on every tile: the pointer crosses tiles constantly, and a per-tile prop would
 * re-render the tiles it left and entered each time. Pointer events pass straight through it, so it never steals a click or a hold from the tile beneath.
 *
 * Mount only while forces are selected. Draws nothing off the map, on an unobserved tile, on the selected forces' own tile, or on a tile with no level.
 */
export default function TileSupportMarker({ hoveredTileStore, supportByTileKey, observedTileKeys, selectedForceTileKeys }: TileSupportMarkerProps) {
  const hoveredTile = useHoveredTile(hoveredTileStore);

  if (hoveredTile == null || supportByTileKey == null) {
    return null;
  }

  const tileKey = `${hoveredTile.x},${hoveredTile.y}`;

  if (!observedTileKeys.has(tileKey) || selectedForceTileKeys.has(tileKey)) {
    return null;
  }

  const support = supportByTileKey.get(tileKey);

  if (support == null) {
    return null;
  }

  const className = ["tile-support-marker", tileSupportLevelClass("tile-support-marker", support.supportLevel)].filter(Boolean).join(" ");

  return (
    <span
      className={className}
      role="img"
      aria-label={supportTurnsLabel(support.supportLevel, support.turns)}
      // Centred on the tile: the marker's own transform pulls it back by half its size.
      style={{ left: hoveredTile.x * TILE_SIZE_PX + TILE_SIZE_PX / 2, top: hoveredTile.y * TILE_SIZE_PX + TILE_SIZE_PX / 2 }}
    >
      {/* Hidden from assistive technology: the label above already says it in words. */}
      <span className="tile-support-marker__turns" aria-hidden="true">
        {formatSupportTurns(support.turns)}
      </span>
    </span>
  );
}
