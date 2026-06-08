import { useLayoutEffect, useRef, useState, type ReactNode } from "react";

interface TooltipProps {
  content: string | undefined;
  children: ReactNode;
  className?: string;
}

export default function Tooltip({ content, children, className }: TooltipProps) {
  const [visible, setVisible] = useState(false);
  const [shiftPx, setShiftPx] = useState(0);
  const rootRef = useRef<HTMLSpanElement | null>(null);
  const bubbleRef = useRef<HTMLSpanElement | null>(null);

  useLayoutEffect(() => {
    if (!visible) {
      setShiftPx(0);
      return;
    }

    const reposition = () => {
      const bubble = bubbleRef.current;
      if (!bubble) return;

      const rect = bubble.getBoundingClientRect();
      const viewportWidth = window.innerWidth;
      const pad = 8;

      let shift = 0;

      if (rect.left < pad) {
        shift += pad - rect.left;
      }

      if (rect.right > viewportWidth - pad) {
        shift -= rect.right - (viewportWidth - pad);
      }

      setShiftPx(Math.round(shift));
    };

    reposition();
    window.addEventListener("resize", reposition);
    window.addEventListener("scroll", reposition, { passive: true });

    return () => {
      window.removeEventListener("resize", reposition);
      window.removeEventListener("scroll", reposition);
    };
  }, [visible, content]);

  if (!content) return <>{children}</>;

  return (
    <span
      ref={rootRef}
      className={["relative inline-flex", className].filter(Boolean).join(" ")}
      onMouseEnter={() => setVisible(true)}
      onMouseLeave={() => setVisible(false)}>
      {children}
      {visible && (
        <span
          ref={bubbleRef}
          role="tooltip"
          style={{ transform: `translateX(calc(-50% + ${shiftPx}px))` }}
          className="pointer-events-none absolute bottom-full left-1/2 z-50 mb-1 whitespace-nowrap rounded bg-black px-2 py-1 text-xs text-white">
          {content}
        </span>
      )}
    </span>
  );
}
