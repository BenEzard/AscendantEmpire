import { useState, useSyncExternalStore } from "react";

export interface HoveredTileCoords {
  x: number;
  y: number;
}

/**
 * Where the pointer is on the map, held outside React state.
 *
 * The pointer crosses a tile boundary far more often than anything else on the page changes. Held in the page's state, every crossing would re-render the
 * whole game page; held here, only the components that subscribe (the hover marker) render, and the grid's tiles are never touched.
 */
export interface HoveredTileStore {
  /** The tile under the pointer, or null when the pointer is off the map. The same object until the pointer moves to another tile. */
  get: () => HoveredTileCoords | null;
  /** Records the tile under the pointer. Listeners are only told when it is a different tile. */
  set: (coords: HoveredTileCoords | null) => void;
  subscribe: (listener: () => void) => () => void;
}

export function createHoveredTileStore(): HoveredTileStore {
  let current: HoveredTileCoords | null = null;
  const listeners = new Set<() => void>();

  return {
    get: () => current,
    set: (coords) => {
      // Moving within one tile, or leaving the map twice, is no change — telling listeners would only render them for nothing.
      if (coords?.x === current?.x && coords?.y === current?.y) {
        return;
      }

      current = coords == null ? null : { x: coords.x, y: coords.y };
      listeners.forEach((listener) => listener());
    },
    subscribe: (listener) => {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
  };
}

/** One store for the lifetime of the component, so its identity never changes and handlers built on it stay stable. */
export function useHoveredTileStore(): HoveredTileStore {
  const [store] = useState(createHoveredTileStore);
  return store;
}

/** The tile under the pointer, re-rendering the caller only when it changes. */
export function useHoveredTile(store: HoveredTileStore): HoveredTileCoords | null {
  return useSyncExternalStore(store.subscribe, store.get);
}
