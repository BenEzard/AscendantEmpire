import type { PlayerMapResource } from "@/features/map/api/playerMapResourceApi";
import type { TileMapResourceMarker } from "@/features/map/components/TileTypes";
import { formatEstimatedAmount } from "@/util/number";

/**
 * Groups the player's discovered MapResources by the tile they sit on, in slot order.
 *
 * Built once per read so the grid can look a tile up rather than scan the list, and so each tile is handed the same array identity on every render it does
 * not change in — a fresh array per tile would defeat the memo that keeps the map from re-rendering wholesale.
 *
 * Nothing is filtered out here, including a resource whose type has no icon: the tile decides what to draw, and a no-icon resource still belongs in the
 * tile's hover title.
 *
 * @returns A map keyed `"x,y"`, as the rest of the grid keys tiles.
 */
export function groupMapResourcesByTile(resources: readonly PlayerMapResource[]): ReadonlyMap<string, TileMapResourceMarker[]> {
  const byTile = new Map<string, TileMapResourceMarker[]>();

  for (const resource of resources) {
    const key = `${resource.x},${resource.y}`;

    const marker: TileMapResourceMarker = {
      slot: resource.slot,
      name: resource.displayName,
      iconPath: resource.iconPath,
      estimatedQuantity: resource.estimatedQuantity,
    };

    const onTile = byTile.get(key);

    if (onTile) {
      onTile.push(marker);
    } else {
      byTile.set(key, [marker]);
    }
  }

  // The server already orders by tile and slot; sorted again here so a tile's drawing order is this module's promise rather than the read's.
  for (const onTile of byTile.values()) {
    onTile.sort((first, second) => first.slot - second.slot);
  }

  return byTile;
}

/**
 * Names a tile's MapResources the way the hover title and the toolbar's tooltips both say it: "Berries (~1.2k), Copper".
 *
 * A resource with no estimate is named alone rather than with a 0 or a dash. There is a real difference between a patch the player cannot see the size of
 * and one that has been stripped bare, and only the second is a number.
 */
export function describeMapResources(markers: readonly TileMapResourceMarker[]): string {
  return markers.map((marker) => describeMapResource(marker)).join(", ");
}

/**
 * One resource named with its estimate, for a tooltip of its own.
 *
 * Tilded, because a quantity the player is shown for a patch is always their own fuzzed estimate of it and never the true amount.
 */
export function describeMapResource(marker: TileMapResourceMarker): string {
  return marker.estimatedQuantity == null ? marker.name : `${marker.name} (${formatEstimatedAmount(marker.estimatedQuantity)})`;
}
