import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useAnchoredOverlay } from "@/hooks/useAnchoredOverlay";
import type { PlayerResource, PlayerResourceMetaCategory } from "@/features/game/api/playerResourcesApi";
import { mapMetaCategoryToTickerCategories } from "../resources/tickers/mapPlayerResources";
import type { ResourceTickerData } from "../resources/tickers/ResourceTickerCollection";
import { playerResourcesKey, usePlayerResourcesQuery } from "./usePlayerResources";

/**
 * DOM id of one meta-category's toolbar button: its panel anchors to that button, and the outside-click check exempts it.
 *
 * Meta-category codes are PascalCase, so the code is slugged rather than interpolated raw.
 */
export function resourceToolbarButtonDomId(metaCategoryCode: string): string {
  return `toolbar-btn-resource-${metaCategoryCode.toLowerCase().replace(/[^a-z0-9]+/g, "-")}`;
}

const RESOURCE_OVERLAY_OFFSET_Y = 20;

/** Stable empty list for the render before the query lands, so the toolbar's memo is not rebuilt every render while it is loading. */
const EMPTY_META_CATEGORIES: PlayerResourceMetaCategory[] = [];

interface UseResourcePanelsArgs {
  gameId: number | undefined;
  playerId: number | undefined;
  /** Whether a game is open at all — the same gate every other game query uses. */
  enabled: boolean;
}

/**
 * Owns the Resources toolbar group and its panels: the Player's unlocked meta-categories, which one panel is open, that panel's anchored position and
 * outside-click dismissal, and the resource-manager dialog state.
 *
 * One panel is open at a time, keyed by meta-category code. The panel is built from the open meta-category alone, so opening a different button shows
 * a different set of categories from the same cached tree rather than refetching.
 */
export function useResourcePanels({ gameId, playerId, enabled }: UseResourcePanelsArgs) {
  const queryClient = useQueryClient();
  const { data } = usePlayerResourcesQuery(gameId, playerId, enabled);
  const metaCategories = data ?? EMPTY_META_CATEGORIES;

  const [openMetaCategoryCode, setOpenMetaCategoryCode] = useState<string | null>(null);
  const [showResourceManager, setShowResourceManager] = useState(false);
  const [resourceManagerSelectedId, setResourceManagerSelectedId] = useState<string | null>(null);
  const overlayRef = useRef<HTMLDivElement | null>(null);

  // Null when the open code no longer matches anything, which closes the panel implicitly if a meta-category is lost between refetches.
  const openMetaCategory = useMemo(
    () => metaCategories.find((metaCategory) => metaCategory.code === openMetaCategoryCode) ?? null,
    [metaCategories, openMetaCategoryCode],
  );

  // Only the open panel is mapped, and only when the tree's identity changes: react-query's structural sharing holds it across a refetch that
  // returned the same data.
  const openCategories = useMemo(
    () => (openMetaCategory != null ? mapMetaCategoryToTickerCategories(openMetaCategory) : []),
    [openMetaCategory],
  );

  const { top: overlayTop, left: overlayLeft } = useAnchoredOverlay(
    openMetaCategoryCode != null ? resourceToolbarButtonDomId(openMetaCategoryCode) : "",
    openMetaCategoryCode != null,
    RESOURCE_OVERLAY_OFFSET_Y,
  );

  // Close the open panel when clicking outside of it (and outside its own button).
  useEffect(() => {
    if (openMetaCategoryCode == null) {
      return;
    }

    const openButtonDomId = resourceToolbarButtonDomId(openMetaCategoryCode);

    const onPointerDown = (e: PointerEvent) => {
      const target = e.target;
      if (!(target instanceof Node)) {
        return;
      }

      const overlayEl = overlayRef.current;
      if (overlayEl && overlayEl.contains(target)) {
        return;
      }

      const buttonEl = document.getElementById(openButtonDomId);
      if (buttonEl && buttonEl.contains(target)) {
        return;
      }

      setOpenMetaCategoryCode(null);
    };

    // Capture phase so we close even if inner elements stop propagation.
    document.addEventListener("pointerdown", onPointerDown, true);
    return () => document.removeEventListener("pointerdown", onPointerDown, true);
  }, [openMetaCategoryCode]);

  /**
   * Opens a meta-category's panel, or closes it if it is already the open one.
   *
   * The functional updater is load-bearing now that there are several buttons. Only the open panel's own button is exempt from the outside-click
   * handler, so clicking a different button closes the open panel first (capture-phase pointerdown runs before the button's click) and then this
   * runs. Reading `openMetaCategoryCode` from the render scope instead would toggle against a value that may already be stale.
   */
  const toggleResourcePanel = useCallback((metaCategoryCode: string) => {
    setOpenMetaCategoryCode((previous) => (previous === metaCategoryCode ? null : metaCategoryCode));
  }, []);

  /**
   * Re-orders one group's resources after a drag, by patching the cached tree rather than holding an order of its own.
   *
   * The order is the resources' intended usage order, and nothing persists it yet, so any refetch — the end of a turn, the window regaining focus —
   * discards it. A later ticket stores it on the player tables.
   */
  const reorderGroupTickers = useCallback(
    ({ categoryCode, groupCode, tickers }: { categoryCode: string; groupCode: string; tickers: ResourceTickerData[] }) => {
      queryClient.setQueryData<PlayerResourceMetaCategory[]>(playerResourcesKey(gameId, playerId), (previous) => {
        if (previous == null) {
          return previous;
        }

        return previous.map((metaCategory) => ({
          ...metaCategory,
          categories: metaCategory.categories.map((category) => {
            if (category.code !== categoryCode) {
              return category;
            }

            return {
              ...category,
              groups: category.groups.map((group) => {
                if (group.code !== groupCode) {
                  return group;
                }

                // Rebuilt from the dragged order by code. Anything the drag did not name keeps its place at the end, so an order that arrived from a
                // stale render cannot drop a resource out of the group.
                const resourcesByCode = new Map(group.resources.map((resource) => [resource.code, resource]));
                const reordered: PlayerResource[] = [];

                for (const ticker of tickers) {
                  const resource = resourcesByCode.get(ticker.code);
                  if (resource != null) {
                    reordered.push(resource);
                    resourcesByCode.delete(ticker.code);
                  }
                }

                return { ...group, resources: [...reordered, ...resourcesByCode.values()] };
              }),
            };
          }),
        }));
      });
    },
    [queryClient, gameId, playerId],
  );

  // Every ticker in the open panel, which is what the resource-manager dialog picks from — it is opened by clicking one of them.
  const allOpenPanelTickers = useMemo<ResourceTickerData[]>(() => {
    const result: ResourceTickerData[] = [];
    for (const category of openCategories) {
      if (category.type === "grouped-resources") {
        for (const group of category.tickerGroups) {
          result.push(...group.tickers);
        }
      } else if (category.type === "standalone-resources") {
        result.push(...category.resources);
      }
    }
    return result;
  }, [openCategories]);

  return {
    metaCategories,
    openMetaCategoryCode,
    openMetaCategory,
    openCategories,
    toggleResourcePanel,
    reorderGroupTickers,
    overlayRef,
    overlayTop,
    overlayLeft,
    allOpenPanelTickers,
    showResourceManager,
    setShowResourceManager,
    resourceManagerSelectedId,
    setResourceManagerSelectedId,
  };
}
