import { useEffect, useState } from "react";

export interface OverlayPosition {
  top: number;
  left: number;
}

/**
 * Computes a fixed-overlay position anchored to the bottom-left of a DOM element
 * (looked up by id) while `open` is true, keeping it in sync on resize/scroll.
 * `offsetY` is added below the anchor's bottom edge.
 */
export function useAnchoredOverlay(anchorDomId: string, open: boolean, offsetY = 0): OverlayPosition {
  const [position, setPosition] = useState<OverlayPosition>({ top: 0, left: 0 });

  useEffect(() => {
    if (!open) return;

    const compute = () => {
      const anchorEl = document.getElementById(anchorDomId);
      if (!anchorEl) return;
      const rect = anchorEl.getBoundingClientRect();
      // fixed overlay; rect is viewport-relative
      setPosition({ top: Math.round(rect.bottom + offsetY), left: Math.round(rect.left) });
    };

    compute();
    window.addEventListener("resize", compute);
    window.addEventListener("scroll", compute, { passive: true });
    return () => {
      window.removeEventListener("resize", compute);
      window.removeEventListener("scroll", compute);
    };
  }, [anchorDomId, open, offsetY]);

  return position;
}
