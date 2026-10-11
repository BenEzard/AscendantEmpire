import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { DOMAttributes, KeyboardEvent, RefObject } from "react";

// Pan and zoom for one large SVG, driven by its viewBox.
//
// A viewBox rather than a scroll container because a scroll container cannot zoom, and the technology tree is thousands of pixels across in both directions —
// without zoom its shape is never visible. The usual middle road, a scroll container with a CSS transform on the inner SVG, needs a sizing wrapper kept in
// step with the scaled dimensions and a manual scroll-offset conversion on every zoom to keep the cursor anchored: the same arithmetic as a viewBox, with
// blurrier text and two sources of truth. The cost of this choice is that wheel and drag are implemented by hand, and that there are no native scrollbars —
// keyboard access is provided by the outline rail rather than by scrolling.

/** The content rectangle currently on screen, in content coordinates. Exactly what goes into the viewBox attribute. */
export interface SvgViewport {
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface UseSvgViewportOptions {
  /** How far out the viewer may zoom. The default shows a canvas a few thousand pixels wide in a normal pane. */
  minScale?: number;
  /** How far in the viewer may zoom. */
  maxScale?: number;
  /**
   * The container's size, for environments that have no layout.
   *
   * jsdom reports every element as zero by zero, so without this the maths under test would divide by zero and the hook could not be unit-tested at all. In
   * the app it is only a starting value: the ResizeObserver reading replaces it as soon as there is one.
   */
  initialContainerSize?: { width: number; height: number };
}

export interface UseSvgViewportResult {
  viewport: SvgViewport;
  /** The viewBox attribute string, ready to spread onto the svg. */
  viewBox: string;
  /** Rendered pixels per content pixel. One means the canvas is at its natural size. */
  scale: number;
  isPanning: boolean;
  containerRef: RefObject<HTMLDivElement | null>;
  svgRef: RefObject<SVGSVGElement | null>;
  pointerHandlers: Pick<DOMAttributes<SVGSVGElement>, "onPointerDown" | "onPointerMove" | "onPointerUp" | "onPointerCancel">;
  /**
   * Whether the pointer sequence that just finished moved far enough to count as a pan.
   *
   * Click handlers have to consult this and bail: without it every pan ends by selecting whatever the pointer happened to come to rest on, which is the
   * classic bug in a hand-rolled canvas.
   */
  didPan: () => boolean;
  /** Frames the whole canvas. What the viewer opens on — you should see the shape before you try to read anything. */
  fitToContent: () => void;
  /** Back to natural size, at the canvas's top-left corner. Where the labels are readable. */
  resetZoom: () => void;
  zoomBy: (factor: number, anchorClient?: { clientX: number; clientY: number }) => void;
  /** Brings a content point to the middle of the view, without changing the zoom. Used by the search and the outline rail. */
  centreOn: (point: { x: number; y: number }) => void;
  /** Keyboard panning and zooming for the focused container: arrows pan, plus and minus zoom, 0 fits, 1 resets. */
  handleKeyDown: (event: KeyboardEvent) => void;
}

/** What the viewport stores. Height is always derived from the container's aspect ratio, so it is not state. */
interface ViewState {
  x: number;
  y: number;
  width: number;
}

const DEFAULT_MIN_SCALE = 0.15;
const DEFAULT_MAX_SCALE = 2.5;

/** The container size assumed until something measures one. Only ever seen in an environment with no layout. */
const ASSUMED_CONTAINER = { width: 1200, height: 800 };

/** How far the pointer must travel during a drag before the gesture counts as a pan rather than a click. */
const PAN_THRESHOLD_PX = 4;

/** How much of the view one arrow-key press moves. */
const KEYBOARD_PAN_FRACTION = 0.1;

/** The zoom step one plus or minus press applies. */
const KEYBOARD_ZOOM_STEP = 1.2;

/** Converts wheel delta into a zoom factor. Small enough that a single notch is a nudge rather than a jump. */
const WHEEL_ZOOM_SENSITIVITY = 0.0015;

/** The same, for a trackpad pinch, which arrives as ctrl or meta plus wheel with much smaller deltas. */
const PINCH_ZOOM_SENSITIVITY = 0.01;

export function useSvgViewport(content: { width: number; height: number }, options?: UseSvgViewportOptions): UseSvgViewportResult {
  const minScale = options?.minScale ?? DEFAULT_MIN_SCALE;
  const maxScale = options?.maxScale ?? DEFAULT_MAX_SCALE;

  const containerRef = useRef<HTMLDivElement | null>(null);
  const svgRef = useRef<SVGSVGElement | null>(null);

  const [containerSize, setContainerSize] = useState(options?.initialContainerSize ?? ASSUMED_CONTAINER);
  const [view, setView] = useState<ViewState>({ x: 0, y: 0, width: options?.initialContainerSize?.width ?? ASSUMED_CONTAINER.width });
  const [isPanning, setIsPanning] = useState(false);

  // A pan is read and written from inside pointer handlers, which must not re-render on every move, so the gesture lives entirely in refs.
  const panOriginRef = useRef<{ clientX: number; clientY: number } | null>(null);
  const panDistanceRef = useRef(0);
  const didPanRef = useRef(false);

  const containerWidth = containerSize.width > 0 ? containerSize.width : ASSUMED_CONTAINER.width;
  const containerHeight = containerSize.height > 0 ? containerSize.height : ASSUMED_CONTAINER.height;
  const aspect = containerHeight / containerWidth;

  const viewport = useMemo<SvgViewport>(
    () => ({ x: view.x, y: view.y, width: view.width, height: view.width * aspect }),
    [view.x, view.y, view.width, aspect],
  );

  const scale = containerWidth / view.width;

  /** The widest and narrowest the view may be, which is how the scale limits are actually enforced. */
  const widthLimits = useMemo(
    () => ({ narrowest: containerWidth / maxScale, widest: containerWidth / minScale }),
    [containerWidth, maxScale, minScale],
  );

  const clampWidth = useCallback(
    (width: number) => Math.min(widthLimits.widest, Math.max(widthLimits.narrowest, width)),
    [widthLimits],
  );

  // ── Measuring the container ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

  useEffect(() => {
    const container = containerRef.current;

    if (container === null) {
      return;
    }

    const measure = () => {
      const rect = container.getBoundingClientRect();

      // Zero means either no layout at all or a hidden container; either way the previous reading is a better guess than zero.
      if (rect.width > 0 && rect.height > 0) {
        setContainerSize({ width: rect.width, height: rect.height });
      }
    };

    measure();

    // Guarded rather than assumed, so a test environment without the global needs no stub of its own.
    if (typeof ResizeObserver === "undefined") {
      return;
    }

    const observer = new ResizeObserver(measure);
    observer.observe(container);

    return () => observer.disconnect();
  }, []);

  // ── The commands ──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

  const fitToContent = useCallback(() => {
    if (content.width <= 0 || content.height <= 0) {
      return;
    }

    // Wide enough to show the whole canvas horizontally, and tall enough — once the container's aspect ratio is applied — to show it vertically too.
    const width = clampWidth(Math.max(content.width, content.height / aspect));
    const height = width * aspect;

    setView({ x: (content.width - width) / 2, y: (content.height - height) / 2, width });
  }, [content.width, content.height, aspect, clampWidth]);

  const resetZoom = useCallback(() => {
    setView({ x: 0, y: 0, width: clampWidth(containerWidth) });
  }, [clampWidth, containerWidth]);

  const zoomBy = useCallback(
    (factor: number, anchorClient?: { clientX: number; clientY: number }) => {
      setView((current) => {
        // Clamped before the anchor maths, not after, so the anchor stays exact even at the limit rather than drifting as the zoom is cut short.
        const width = clampWidth(current.width / factor);

        if (width === current.width) {
          return current;
        }

        const height = width * aspect;
        const currentHeight = current.width * aspect;

        // Where in the view the anchor sits, as a fraction. The centre when there is no anchor, or when there is no layout to measure against.
        let fractionX = 0.5;
        let fractionY = 0.5;

        if (anchorClient !== undefined) {
          const rect = containerRef.current?.getBoundingClientRect();

          if (rect !== undefined && rect.width > 0 && rect.height > 0) {
            fractionX = (anchorClient.clientX - rect.left) / rect.width;
            fractionY = (anchorClient.clientY - rect.top) / rect.height;
          }
        }

        // Keep whatever content was under that fraction of the view exactly where it was.
        const anchorContentX = current.x + fractionX * current.width;
        const anchorContentY = current.y + fractionY * currentHeight;

        return { x: anchorContentX - fractionX * width, y: anchorContentY - fractionY * height, width };
      });
    },
    [aspect, clampWidth],
  );

  const centreOn = useCallback(
    (point: { x: number; y: number }) => {
      setView((current) => ({ x: point.x - current.width / 2, y: point.y - (current.width * aspect) / 2, width: current.width }));
    },
    [aspect],
  );

  // ── Opening on a fitted view ──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
  //
  // The canvas is empty on the first render, while the catalogue is still loading, so there is nothing to fit to yet. Fit once, as soon as there is.

  const fittedContentRef = useRef<string | null>(null);

  useEffect(() => {
    const signature = `${content.width}x${content.height}`;

    if (content.width <= 0 || content.height <= 0 || fittedContentRef.current === signature) {
      return;
    }

    fittedContentRef.current = signature;
    fitToContent();
  }, [content.width, content.height, fitToContent]);

  // ── Wheel ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
  //
  // A native listener registered non-passively, because React's onWheel is passive: calling preventDefault there warns and the page scrolls behind the
  // overlay instead of the canvas zooming.

  useEffect(() => {
    const container = containerRef.current;

    if (container === null) {
      return;
    }

    const onWheel = (event: WheelEvent) => {
      event.preventDefault();

      // A trackpad pinch arrives as ctrl or meta plus wheel, with far smaller deltas than a wheel notch, so it needs its own coefficient to feel the same.
      const sensitivity = event.ctrlKey || event.metaKey ? PINCH_ZOOM_SENSITIVITY : WHEEL_ZOOM_SENSITIVITY;

      zoomBy(Math.exp(-event.deltaY * sensitivity), { clientX: event.clientX, clientY: event.clientY });
    };

    container.addEventListener("wheel", onWheel, { passive: false });

    return () => container.removeEventListener("wheel", onWheel);
  }, [zoomBy]);

  // ── Drag to pan ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

  const pointerHandlers = useMemo<UseSvgViewportResult["pointerHandlers"]>(
    () => ({
      onPointerDown: (event) => {
        // Primary button only, so a right-click or a middle-click never starts a pan.
        if (event.button !== 0) {
          return;
        }

        panOriginRef.current = { clientX: event.clientX, clientY: event.clientY };
        panDistanceRef.current = 0;
        didPanRef.current = false;
        setIsPanning(true);

        // Capture, so a drag that leaves the SVG keeps panning rather than sticking.
        event.currentTarget.setPointerCapture?.(event.pointerId);
      },
      onPointerMove: (event) => {
        const origin = panOriginRef.current;

        if (origin === null) {
          return;
        }

        const screenDeltaX = event.clientX - origin.clientX;
        const screenDeltaY = event.clientY - origin.clientY;

        panOriginRef.current = { clientX: event.clientX, clientY: event.clientY };
        panDistanceRef.current += Math.abs(screenDeltaX) + Math.abs(screenDeltaY);

        if (panDistanceRef.current > PAN_THRESHOLD_PX) {
          didPanRef.current = true;
        }

        // Screen pixels into content pixels: dragging right moves the view left, so the content follows the pointer.
        setView((current) => {
          const contentPerScreenPixel = current.width / containerWidth;

          return { x: current.x - screenDeltaX * contentPerScreenPixel, y: current.y - screenDeltaY * contentPerScreenPixel, width: current.width };
        });
      },
      onPointerUp: (event) => {
        panOriginRef.current = null;
        setIsPanning(false);
        event.currentTarget.releasePointerCapture?.(event.pointerId);
      },
      onPointerCancel: () => {
        panOriginRef.current = null;
        setIsPanning(false);
      },
    }),
    [containerWidth],
  );

  const didPan = useCallback(() => didPanRef.current, []);

  // ── Keyboard ──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

  const handleKeyDown = useCallback(
    (event: KeyboardEvent) => {
      const panBy = (deltaX: number, deltaY: number) => {
        setView((current) => ({
          x: current.x + deltaX * current.width * KEYBOARD_PAN_FRACTION,
          y: current.y + deltaY * current.width * aspect * KEYBOARD_PAN_FRACTION,
          width: current.width,
        }));
      };

      switch (event.key) {
        case "ArrowLeft":
          panBy(-1, 0);
          break;
        case "ArrowRight":
          panBy(1, 0);
          break;
        case "ArrowUp":
          panBy(0, -1);
          break;
        case "ArrowDown":
          panBy(0, 1);
          break;
        case "+":
        case "=":
          zoomBy(KEYBOARD_ZOOM_STEP);
          break;
        case "-":
        case "_":
          zoomBy(1 / KEYBOARD_ZOOM_STEP);
          break;
        case "0":
          fitToContent();
          break;
        case "1":
          resetZoom();
          break;
        default:
          // Anything else belongs to whatever else is listening — Escape in particular, which the viewer handles.
          return;
      }

      event.preventDefault();
    },
    [aspect, zoomBy, fitToContent, resetZoom],
  );

  return {
    viewport,
    viewBox: `${viewport.x} ${viewport.y} ${viewport.width} ${viewport.height}`,
    scale,
    isPanning,
    containerRef,
    svgRef,
    pointerHandlers,
    didPan,
    fitToContent,
    resetZoom,
    zoomBy,
    centreOn,
    handleKeyDown,
  };
}
