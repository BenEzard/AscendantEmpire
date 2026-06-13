import { useEffect, useState, type RefObject } from "react";

/**
 * Tracks the viewport-relative bottom edge (in px) of the referenced element,
 * updating on window resize/scroll and element resize. Useful for sizing a spacer
 * under a fixed-position element.
 */
export function useElementBottomPx(ref: RefObject<HTMLElement | null>): number {
  const [bottomPx, setBottomPx] = useState<number>(0);

  useEffect(() => {
    const compute = () => {
      const el = ref.current;
      if (!el) return;
      setBottomPx(Math.round(el.getBoundingClientRect().bottom));
    };

    compute();
    window.addEventListener("resize", compute);
    window.addEventListener("scroll", compute, { passive: true });

    const el = ref.current;
    if (!el || !("ResizeObserver" in window)) {
      return () => {
        window.removeEventListener("resize", compute);
        window.removeEventListener("scroll", compute);
      };
    }

    const ro = new ResizeObserver(compute);
    ro.observe(el);

    return () => {
      ro.disconnect();
      window.removeEventListener("resize", compute);
      window.removeEventListener("scroll", compute);
    };
  }, [ref]);

  return bottomPx;
}
