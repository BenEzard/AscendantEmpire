import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useAnchoredOverlay } from "@/hooks/useAnchoredOverlay";
import { resourceDummyData } from "../resources/tickers/ResourceDummyData";
import type { ResourceCategoryData, ResourceTickerData } from "../resources/tickers/ResourceTickerCollection";

/** DOM id of the toolbar button the construction overlay anchors to / ignores on outside-click. */
export const CONSTRUCTION_BUTTON_DOM_ID = "toolbar-btn-construction-material";

const CONSTRUCTION_OVERLAY_OFFSET_Y = 20;

/**
 * Owns the construction (resource ticker) overlay: visibility, anchored position,
 * outside-click dismissal, the category data, and the resource-manager dialog state.
 */
export function useConstructionPanel() {
  const [showConstructionPanel, setShowConstructionPanel] = useState(false);
  const [constructionCategories, setConstructionCategories] = useState<ResourceCategoryData[]>(() => resourceDummyData);
  const [showResourceManager, setShowResourceManager] = useState(false);
  const [resourceManagerSelectedId, setResourceManagerSelectedId] = useState<string | null>(null);
  const overlayRef = useRef<HTMLDivElement | null>(null);

  const { top: overlayTop, left: overlayLeft } = useAnchoredOverlay(CONSTRUCTION_BUTTON_DOM_ID, showConstructionPanel, CONSTRUCTION_OVERLAY_OFFSET_Y);

  // Close the construction popup when clicking outside of it (and outside its button).
  useEffect(() => {
    if (!showConstructionPanel) return;

    const onPointerDown = (e: PointerEvent) => {
      const target = e.target;
      if (!(target instanceof Node)) return;

      const overlayEl = overlayRef.current;
      if (overlayEl && overlayEl.contains(target)) return;

      const buttonEl = document.getElementById(CONSTRUCTION_BUTTON_DOM_ID);
      if (buttonEl && buttonEl.contains(target)) return;

      setShowConstructionPanel(false);
    };

    // Capture phase so we close even if inner elements stop propagation.
    document.addEventListener("pointerdown", onPointerDown, true);
    return () => document.removeEventListener("pointerdown", onPointerDown, true);
  }, [showConstructionPanel]);

  const allConstructionTickers = useMemo<ResourceTickerData[]>(() => {
    const result: ResourceTickerData[] = [];
    for (const category of constructionCategories) {
      if (category.type === "processed-resources") {
        for (const group of category.tickerGroups) {
          result.push(...group.tickers);
        }
      } else if (category.type === "standalone-resources") {
        result.push(...category.resources);
      }
    }
    return result;
  }, [constructionCategories]);

  const toggleConstructionPanel = useCallback(() => {
    setShowConstructionPanel((v) => !v);
  }, []);

  return {
    showConstructionPanel,
    toggleConstructionPanel,
    overlayRef,
    overlayTop,
    overlayLeft,
    constructionCategories,
    setConstructionCategories,
    allConstructionTickers,
    showResourceManager,
    setShowResourceManager,
    resourceManagerSelectedId,
    setResourceManagerSelectedId,
  };
}
