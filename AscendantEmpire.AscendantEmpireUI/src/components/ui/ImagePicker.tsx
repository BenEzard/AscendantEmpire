import { useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import Tooltip from "./Tooltip";

export interface ImagePickerItem {
  /** The value raised to `onChange` when this item is chosen. Compared by exact string. */
  value: string;

  /** The item's accessible name and tooltip, e.g. "Portrait 7". Also what a tile shows if its image fails to load. */
  label: string;

  /** The image to draw. Defaults to `value`, which is what you want whenever the stored value *is* the image URL. */
  src?: string;
}

/**
 * An image already claimed elsewhere and therefore not pickable again.
 *
 * The same shape as the colour picker's `UsedColor`, so a caller enforcing "one each" over images reads the same as one
 * enforcing it over colours.
 */
export interface UsedImage {
  /** The claimed value, compared against each item's `value` exactly. */
  value: string;

  /** What already uses it, surfaced as "Already used by `{usedBy}`" on hover. */
  usedBy: string;
}

export interface ImagePickerProps {
  /** The chosen value, or null or empty when nothing has been chosen yet. */
  value?: string | null;

  /** Raised with the chosen item's `value`. Never raised with null - this picker offers no "none" choice. */
  onChange: (value: string) => void;

  /** The images offered, rendered as a grid in the order given. */
  items: readonly ImagePickerItem[];

  /**
   * Values claimed elsewhere, which are struck through and made unpickable.
   *
   * The current `value` is exempt, so re-editing whatever claimed it still shows its own selection as available - a
   * caller does not have to filter itself out of the list it passes.
   */
  usedValues?: readonly UsedImage[];

  /**
   * What the trigger shows when nothing is chosen, in place of the default "?".
   *
   * For a field where "nothing chosen" has a meaning of its own worth drawing - a crown standing in for a ruler with
   * no portrait, say - rather than reading as an empty box.
   */
  emptyIcon?: React.ReactNode;

  /** How many tiles per row before wrapping. */
  columns?: number;

  /** Tile width in pixels inside the grid. Its height follows from `aspectRatio`. */
  thumbnailWidth?: number;

  /** Width in pixels of the closed trigger thumbnail. Its height follows from `aspectRatio`. */
  triggerWidth?: number;

  /**
   * Width divided by height of the trigger and of every tile.
   *
   * Set on the boxes rather than left to each image, so a tile has a definite height from its first layout pass -
   * before any image has loaded. The grid's size has to be measurable then, because that is when the popover decides
   * whether to open below the trigger or flip above it. An image is drawn `object-contain` inside its box, so one
   * whose own ratio differs is letterboxed rather than cropped or stretched.
   */
  aspectRatio?: number;

  /** Hover text for the trigger, and the accessible name of both the trigger and the grid. */
  title?: string;

  disabled?: boolean;

  /** Extra classes for the outermost wrapper. */
  className?: string;
}

/**
 * Picks one image from a fixed set: a thumbnail of the current choice that opens a grid of every option.
 *
 * Shaped like ColorPicker rather than laid out inline, for the same reason: a field whose options are pictures
 * would otherwise dominate whatever form it sits in. The grid is portaled and fixed-positioned so it can escape a
 * scrollable or clipping ancestor - a fixed-height dialog page, typically - and is repositioned to stay anchored to
 * its trigger.
 *
 * Knows nothing about what the images are of. The caller supplies the items and stores whatever `value` comes back.
 */
export default function ImagePicker({
  value,
  onChange,
  items,
  usedValues,
  emptyIcon,
  columns = 5,
  thumbnailWidth = 96,
  triggerWidth = 56,
  aspectRatio = 4 / 3,
  title,
  disabled = false,
  className = "",
}: ImagePickerProps) {
  const [open, setOpen] = useState(false);
  const [position, setPosition] = useState<{ top: number; left: number }>({ top: 0, left: 0 });

  // Sources whose image failed to load, so a broken file degrades to its label instead of a broken-image icon.
  // Keyed by src rather than by index, so the set stays correct if `items` is reordered.
  const [failedSources, setFailedSources] = useState<ReadonlySet<string>>(new Set());

  const triggerRef = useRef<HTMLButtonElement | null>(null);
  const popupRef = useRef<HTMLDivElement | null>(null);

  const effectiveColumns = Math.max(1, Math.floor(columns));

  // Empty counts as unchosen, not as a value to look up: a row predating the column it is stored in comes back "".
  const hasValue = typeof value === "string" && value !== "";

  const selectedItem = useMemo(() => (hasValue ? items.find((item) => item.value === value) : undefined), [hasValue, items, value]);

  // Indexed by value, so each tile costs one lookup rather than a scan of the list.
  const usedByValue = useMemo(() => new Map((usedValues ?? []).map((used) => [used.value, used.usedBy])), [usedValues]);

  const triggerSrc = selectedItem ? (selectedItem.src ?? selectedItem.value) : undefined;
  const triggerFailed = triggerSrc !== undefined && failedSources.has(triggerSrc);

  const markFailed = (src: string) => {
    setFailedSources((current) => {
      if (current.has(src)) {
        return current;
      }

      const next = new Set(current);
      next.add(src);

      return next;
    });
  };

  // Close on any pointer press outside the trigger and the (portaled) grid, and on Escape.
  useEffect(() => {
    if (!open) {
      return;
    }

    const onPointerDown = (e: PointerEvent) => {
      if (!(e.target instanceof Node)) {
        return;
      }

      if (triggerRef.current?.contains(e.target) || popupRef.current?.contains(e.target)) {
        return;
      }

      setOpen(false);
    };

    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        e.preventDefault();
        setOpen(false);
        triggerRef.current?.focus();
      }
    };

    document.addEventListener("pointerdown", onPointerDown, { capture: true });
    document.addEventListener("keydown", onKeyDown);

    return () => {
      document.removeEventListener("pointerdown", onPointerDown, { capture: true } as AddEventListenerOptions);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [open]);

  // Anchor the grid under the trigger, flipping above and clamping sideways so no edge of it is cut off.
  //
  // This runs before the first paint, so it measures the grid before a single image has loaded. That reading is only
  // trustworthy because every tile is sized by `aspectRatio` rather than by its image: were the tiles left to size
  // themselves, the grid would measure near-zero here, never flip above, and then overflow the viewport as the images
  // arrived - with nothing scheduled to measure it again.
  useLayoutEffect(() => {
    if (!open) {
      return;
    }

    const reposition = () => {
      const trigger = triggerRef.current;
      const popup = popupRef.current;

      if (!trigger || !popup) {
        return;
      }

      const triggerRect = trigger.getBoundingClientRect();
      const popupRect = popup.getBoundingClientRect();
      const pad = 8;
      const gap = 4;

      // Preferred placement is directly below the trigger.
      let top = triggerRect.bottom + gap;
      if (top + popupRect.height > window.innerHeight - pad) {
        const above = triggerRect.top - gap - popupRect.height;
        // Flip above when there is room there; otherwise pin to the bottom edge, since the grid scrolls internally.
        top = above >= pad ? above : Math.max(pad, window.innerHeight - pad - popupRect.height);
      }

      // Left-aligned with the trigger, then clamped so neither side hangs off the viewport.
      const left = Math.max(pad, Math.min(triggerRect.left, window.innerWidth - popupRect.width - pad));

      setPosition({ top: Math.round(top), left: Math.round(left) });
    };

    reposition();
    window.addEventListener("resize", reposition);
    // Capture phase so scrolling any ancestor - not just the window - keeps the grid pinned to its trigger.
    window.addEventListener("scroll", reposition, { capture: true, passive: true });

    return () => {
      window.removeEventListener("resize", reposition);
      window.removeEventListener("scroll", reposition, { capture: true } as EventListenerOptions);
    };
  }, [open, items, usedValues, effectiveColumns, thumbnailWidth, aspectRatio]);

  const handleTileClick = (item: ImagePickerItem) => {
    onChange(item.value);
    setOpen(false);
  };

  return (
    <span className={["inline-flex align-middle", className].filter(Boolean).join(" ")}>
      <Tooltip content={title}>
        <button
          ref={triggerRef}
          type="button"
          disabled={disabled}
          aria-haspopup="dialog"
          aria-expanded={open}
          aria-label={title ?? "Select image"}
          onClick={() => setOpen((current) => !current)}
          style={{ width: triggerWidth, aspectRatio }}
          className={[
            "flex items-center justify-center overflow-hidden rounded-sm border bg-white leading-none shadow-sm",
            selectedItem && !triggerFailed ? "border-slate-400" : "border-dashed border-slate-400",
            disabled ? "cursor-not-allowed opacity-60" : "cursor-pointer hover:border-slate-600",
          ].join(" ")}>
          {selectedItem && triggerSrc !== undefined && !triggerFailed ? (
            <img src={triggerSrc} alt="" draggable={false} className="h-full w-full object-contain" onError={() => markFailed(triggerSrc)} />
          ) : (
            // Nothing chosen (or the chosen image would not load), so mark the thumbnail as unset rather than
            // leaving an empty box that reads as a deliberate blank. The box is already sized by the button, so
            // this only has to fill it; the fallback "?" is sized off the trigger so it scales with it.
            <span
              aria-hidden="true"
              className="flex h-full w-full items-center justify-center font-semibold text-slate-500"
              style={emptyIcon == null ? { fontSize: Math.round(triggerWidth * 0.4) } : undefined}>
              {emptyIcon ?? "?"}
            </span>
          )}
        </button>
      </Tooltip>

      {open &&
        createPortal(
          <div
            ref={popupRef}
            role="dialog"
            aria-label={title ?? "Image picker"}
            style={{ position: "fixed", top: position.top, left: position.left, maxHeight: "calc(100vh - 16px)" }}
            // Above Modal (z-[1080]) and Drawer (z-[1050]) so the picker still works when hosted inside one,
            // but below Tooltip (z-[1200]) so tile tooltips stay readable over it.
            className="z-[1150] w-max overflow-y-auto rounded-md border border-slate-300 bg-white p-2 shadow-lg">
            <div className="grid gap-1" style={{ gridTemplateColumns: `repeat(${effectiveColumns}, ${thumbnailWidth}px)` }}>
              {items.map((item) => {
                const src = item.src ?? item.value;
                const isSelected = hasValue && item.value === value;
                const hasFailed = failedSources.has(src);
                // The selection is exempt from the used list - whatever claimed it is presumably the very thing being
                // edited, and striking it through would leave the picker with no way back to its own current value.
                const usedBy = isSelected ? undefined : usedByValue.get(item.value);
                const isUsed = usedBy !== undefined;

                return (
                  <Tooltip key={item.value} content={isUsed ? `Already used by \`${usedBy}\`` : item.label}>
                    <button
                      type="button"
                      aria-label={item.label}
                      aria-pressed={isSelected}
                      disabled={isUsed}
                      onClick={isUsed ? undefined : () => handleTileClick(item)}
                      style={{ width: thumbnailWidth, aspectRatio }}
                      className={[
                        "relative flex items-center justify-center overflow-hidden rounded border-2 bg-white p-0.5",
                        isUsed ? "cursor-not-allowed border-slate-200" : "cursor-pointer",
                        isSelected ? "border-selection/55 bg-selection-tint/18" : "",
                        !isSelected && !isUsed ? "border-slate-200 hover:border-selection/40 hover:bg-selection-tint/12" : "",
                      ]
                        .filter(Boolean)
                        .join(" ")}>
                      {hasFailed ? (
                        <span className="flex h-full w-full items-center justify-center px-1 text-center text-[11px] text-slate-500">{item.label}</span>
                      ) : (
                        <img
                          src={src}
                          alt=""
                          draggable={false}
                          loading="lazy"
                          decoding="async"
                          // Faded as well as struck through, so a taken tile reads as unavailable at a glance rather
                          // than only once the line is noticed over a busy image.
                          className={["h-full w-full object-contain", isUsed ? "opacity-40" : ""].filter(Boolean).join(" ")}
                          onError={() => markFailed(src)}
                        />
                      )}

                      {isUsed && (
                        // Diagonal from top-right to bottom-left marking the tile as taken, as the colour picker
                        // strikes a used swatch. Drawn over the image, so it needs a colour that holds against any of
                        // them - hence the white casing around a dark line.
                        <svg className="pointer-events-none absolute inset-0 h-full w-full" viewBox="0 0 10 10" preserveAspectRatio="none" aria-hidden="true">
                          <line x1="10" y1="0" x2="0" y2="10" stroke="white" strokeWidth="3" vectorEffect="non-scaling-stroke" />
                          <line x1="10" y1="0" x2="0" y2="10" stroke="rgb(51 65 85)" strokeWidth="1" vectorEffect="non-scaling-stroke" />
                        </svg>
                      )}
                    </button>
                  </Tooltip>
                );
              })}
            </div>
          </div>,
          document.body,
        )}
    </span>
  );
}
