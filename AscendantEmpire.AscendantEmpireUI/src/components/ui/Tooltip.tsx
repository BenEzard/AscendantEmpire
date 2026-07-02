import { useLayoutEffect, useRef, useState, type ReactNode } from "react";
import { createPortal } from "react-dom";

interface TooltipProps {
  content: string | undefined;
  children: ReactNode;
  className?: string;
}

interface BubbleStyle {
  top: number;
  left: number;
  placement: "top" | "bottom";
}

export default function Tooltip({ content, children, className }: TooltipProps) {
  const [visible, setVisible] = useState(false);
  // Default placement is above the trigger; flips below when there isn't room above (e.g. a toolbar
  // pinned to the top of the viewport).
  const [style, setStyle] = useState<BubbleStyle>({ top: 0, left: 0, placement: "top" });
  const rootRef = useRef<HTMLSpanElement | null>(null);
  const bubbleRef = useRef<HTMLSpanElement | null>(null);

  useLayoutEffect(() => {
    if (!visible) return;

    const reposition = () => {
      const trigger = rootRef.current;
      const bubble = bubbleRef.current;
      if (!trigger || !bubble) return;

      const triggerRect = trigger.getBoundingClientRect();
      const bubbleRect = bubble.getBoundingClientRect();
      const pad = 8;
      const gap = 4;

      const placement: "top" | "bottom" = triggerRect.top - bubbleRect.height - gap < pad ? "bottom" : "top";
      const top = placement === "bottom" ? triggerRect.bottom + gap : triggerRect.top - gap;

      // Centred on the trigger, clamped to the viewport so the bubble never hangs off the browser edge.
      // The bubble is portaled and fixed-positioned (see render below) specifically so it can extend past
      // a scrollable ancestor (e.g. a dialog's content pane) without inflating that ancestor's scrollWidth
      // and popping a horizontal scrollbar — clamping here is purely a viewport concern, not a layout one.
      let left = triggerRect.left + triggerRect.width / 2 - bubbleRect.width / 2;
      left = Math.max(pad, Math.min(left, window.innerWidth - bubbleRect.width - pad));

      setStyle({ top, left, placement });
    };

    reposition();
    window.addEventListener("resize", reposition);
    // Capture phase so scrolling any ancestor (not just the window) repositions the bubble — scroll
    // events don't bubble, but capture-phase listeners on window still see them.
    window.addEventListener("scroll", reposition, { capture: true, passive: true });

    return () => {
      window.removeEventListener("resize", reposition);
      window.removeEventListener("scroll", reposition, { capture: true } as EventListenerOptions);
    };
  }, [visible, content]);

  if (!content) return <>{children}</>;

  return (
    <span ref={rootRef} className={["inline-flex", className].filter(Boolean).join(" ")} onMouseEnter={() => setVisible(true)} onMouseLeave={() => setVisible(false)}>
      {children}
      {visible &&
        createPortal(
          <span
            ref={bubbleRef}
            role="tooltip"
            style={{
              position: "fixed",
              top: style.top,
              left: style.left,
              transform: style.placement === "bottom" ? undefined : "translateY(-100%)",
            }}
            // Portaled to document.body, so this is now a sibling of Modal (z-[1080]), Drawer (z-[1050])
            // and Toast (z-[1100]) rather than nested inside their stacking context — z-50 would render
            // behind them. Sit above all current overlays so a tooltip is never hidden by one.
            className="pointer-events-none z-[1200] whitespace-nowrap rounded bg-black px-2 py-1 text-xs text-white">
            {content}
          </span>,
          document.body,
        )}
    </span>
  );
}
