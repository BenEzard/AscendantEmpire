import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { cssColorToRgba, isValidColor, pickTextColorOnBg } from "@/util/color";
import Tooltip from "../Tooltip";
import { TailwindColors } from "./tailwindColors";
import type { Color, ColorPalette, UsedColor } from "./colorTypes";

export interface ColorPickerProps {
  /** The currently selected colour. Any valid HTML colour; matched against the palettes by parsed value, not string equality. */
  value?: string | null;

  /**
   * Raised when a colour is chosen, either by clicking a swatch or by typing a valid custom colour.
   * `null` is only ever passed when `allowClear` is set and the user clears the selection.
   */
  onChange: (value: string | null, color?: Color) => void;

  /**
   * How many swatches to show per row before wrapping. Shared by every palette so they line up as one grid.
   * Defaults to the number of shades Tailwind publishes per hue, which lays the default palette out one hue per row.
   */
  columns?: number;

  /** The palettes offered for selection, rendered as titled blocks in order. Defaults to the Tailwind colours. */
  palettes?: ColorPalette[];

  /**
   * Colours already claimed elsewhere. These are merged onto the matching palette swatches, which are struck through and
   * made unclickable. The currently selected colour is exempt, so re-editing whatever claimed it still shows its selection.
   */
  usedColors?: UsedColor[];

  /** When true, shows a preview square and a text field accepting any HTML colour. A valid entry becomes the selection. */
  allowCustomColor?: boolean;

  /** When true, offers a "No colour" choice that clears the selection by raising `onChange(null)`. For nullable colour fields. */
  allowClear?: boolean;

  /** Edge length in pixels of each palette swatch. */
  swatchSize?: number;

  /** Edge length in pixels of the closed trigger square. */
  triggerSize?: number;

  /** Hover text for the trigger square. */
  title?: string;

  disabled?: boolean;

  /** Extra classes for the outermost wrapper. */
  className?: string;
}

/**
 * Reduces a colour to a canonical key so that "#fb2c36", "rgb(251, 44, 54)" and "#FB2C36" all compare equal.
 * Falls back to a trimmed, lower-cased string when the browser cannot parse the value.
 */
function colorKey(value?: string | null): string | null {
  const rgba = cssColorToRgba(value);
  if (rgba) {
    return `${rgba.r},${rgba.g},${rgba.b},${rgba.a}`;
  }
  const trimmed = value?.trim().toLowerCase();
  return trimmed ? trimmed : null;
}

export default function ColorPicker({
  value,
  onChange,
  columns = TailwindColors.ShadeCount,
  palettes,
  usedColors,
  allowCustomColor = false,
  allowClear = false,
  swatchSize = 18,
  triggerSize = 24,
  title,
  disabled = false,
  className = "",
}: ColorPickerProps) {
  const [open, setOpen] = useState(false);
  const [position, setPosition] = useState<{ top: number; left: number }>({ top: 0, left: 0 });

  // Text currently in the custom-colour field. Non-empty means the user is driving the selection by hand;
  // clicking a swatch clears it again.
  const [customText, setCustomText] = useState("");

  const triggerRef = useRef<HTMLButtonElement | null>(null);
  const popupRef = useRef<HTMLDivElement | null>(null);

  // Default to the Tailwind palette, but build it once rather than on every render.
  const effectivePalettes = useMemo(() => palettes ?? TailwindColors.defaultPalettes(), [palettes]);

  const selectedKey = useMemo(() => colorKey(value), [value]);

  // Used colours indexed by canonical key, so each swatch costs a single lookup rather than a scan of the list.
  const usedByKey = useMemo(() => {
    const map = new Map<string, string>();
    for (const used of usedColors ?? []) {
      const key = colorKey(used.value);
      if (key) {
        map.set(key, used.usedBy);
      }
    }
    return map;
  }, [usedColors]);

  // True when the selected colour is not one of the offered swatches, i.e. it can only have come from the custom field.
  const selectionIsCustom = useMemo(() => {
    if (!selectedKey) {
      return false;
    }
    for (const palette of effectivePalettes) {
      for (const color of palette.colors) {
        if (colorKey(color.value) === selectedKey) {
          return false;
        }
      }
    }
    return true;
  }, [effectivePalettes, selectedKey]);

  const effectiveColumns = Math.max(1, Math.floor(columns));
  const customTextIsValid = isValidColor(customText);

  // Seed the custom field when opening on a colour no palette offers, so an existing custom value stays editable.
  const openPopup = useCallback(() => {
    setCustomText(allowCustomColor && selectionIsCustom && value ? value : "");
    setOpen(true);
  }, [allowCustomColor, selectionIsCustom, value]);

  // Close on any pointer press outside the trigger and the (portaled) popup, and on Escape.
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

  // Anchor the popup under the trigger, flipping above and clamping sideways so no edge of it is cut off by the viewport.
  // The popup is portaled and fixed-positioned so it can escape a scrollable or clipping ancestor.
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
        // Flip above when there is room there; otherwise pin to the bottom edge (the popup scrolls internally).
        top = above >= pad ? above : Math.max(pad, window.innerHeight - pad - popupRect.height);
      }

      // Left-aligned with the trigger, then clamped so neither side hangs off the viewport.
      const left = Math.max(pad, Math.min(triggerRect.left, window.innerWidth - popupRect.width - pad));

      setPosition({ top: Math.round(top), left: Math.round(left) });
    };

    reposition();
    window.addEventListener("resize", reposition);
    // Capture phase so scrolling any ancestor - not just the window - keeps the popup pinned to its trigger.
    window.addEventListener("scroll", reposition, { capture: true, passive: true });

    return () => {
      window.removeEventListener("resize", reposition);
      window.removeEventListener("scroll", reposition, { capture: true } as EventListenerOptions);
    };
  }, [open, effectivePalettes, effectiveColumns, allowCustomColor, allowClear, swatchSize]);

  const handleSwatchClick = (color: Color) => {
    // A palette pick always wins over whatever is in the custom field.
    setCustomText("");
    onChange(color.value, color);
    setOpen(false);
  };

  const handleClear = () => {
    setCustomText("");
    onChange(null);
    setOpen(false);
  };

  const handleCustomTextChange = (text: string) => {
    setCustomText(text);
    // Only push valid colours upward; a half-typed or bad entry leaves the previous selection alone.
    if (isValidColor(text)) {
      onChange(text);
    }
  };

  const hasValue = isValidColor(value);
  const triggerColor = hasValue ? value! : "#ffffff";

  return (
    <span className={["inline-flex align-middle", className].filter(Boolean).join(" ")}>
      <Tooltip content={title}>
        <button
          ref={triggerRef}
          type="button"
          disabled={disabled}
          aria-haspopup="dialog"
          aria-expanded={open}
          aria-label={title ?? "Select colour"}
          onClick={() => (open ? setOpen(false) : openPopup())}
          style={{ width: triggerSize, height: triggerSize, backgroundColor: triggerColor }}
          className={[
            "flex items-center justify-center rounded-sm border leading-none shadow-sm",
            hasValue ? "border-black/30" : "border-dashed border-slate-400",
            disabled ? "cursor-not-allowed opacity-60" : "cursor-pointer hover:border-black/60",
          ].join(" ")}>
          {/* With nothing selected there is no colour to show, so mark the square as "unset" instead of
              leaving it looking like a deliberate white pick. Sized off the square so it scales with it. */}
          {!hasValue && (
            <span aria-hidden="true" className="font-semibold text-slate-500" style={{ fontSize: Math.round(triggerSize * 0.68) }}>
              ?
            </span>
          )}
        </button>
      </Tooltip>

      {open &&
        createPortal(
          <div
            ref={popupRef}
            role="dialog"
            aria-label="Colour picker"
            style={{ position: "fixed", top: position.top, left: position.left, maxHeight: "calc(100vh - 16px)" }}
            // Above Modal (z-[1080]) and Drawer (z-[1050]) so the picker still works when hosted inside one,
            // but below Tooltip (z-[1200]) so swatch tooltips stay readable over it.
            className="z-[1150] w-max overflow-y-auto rounded-md border border-slate-300 bg-white p-2 shadow-lg">
            {effectivePalettes.map((palette, paletteIndex) => (
              // The title is not drawn - a thin border is what separates one palette from the next - but it is
              // kept as the group's accessible name so the palette is still identifiable to assistive tech.
              <div
                key={`${palette.title}-${paletteIndex}`}
                role="group"
                aria-label={palette.title}
                className="mb-1.5 rounded border border-slate-300 p-1 last:mb-0">
                <div className="grid gap-0.5" style={{ gridTemplateColumns: `repeat(${effectiveColumns}, ${swatchSize}px)` }}>
                  {palette.colors.map((color, colorIndex) => {
                    const key = colorKey(color.value);
                    const isSelected = key !== null && key === selectedKey;
                    // The selected colour is exempt from the used list - it is presumably used by the very thing being edited.
                    const usedBy = isSelected || key === null ? undefined : usedByKey.get(key);
                    const isUsed = usedBy !== undefined;
                    // Selection ring and strike are drawn in whichever of black/white contrasts with the swatch itself.
                    const contrast = pickTextColorOnBg(color.value);

                    return (
                      <Tooltip key={`${color.value}-${colorIndex}`} content={isUsed ? `Already used by \`${usedBy}\`` : color.description}>
                        <button
                          type="button"
                          aria-label={color.description}
                          aria-pressed={isSelected}
                          disabled={isUsed}
                          onClick={isUsed ? undefined : () => handleSwatchClick(color)}
                          style={{
                            width: swatchSize,
                            height: swatchSize,
                            backgroundColor: color.value,
                            boxShadow: isSelected ? `inset 0 0 0 2px ${contrast}` : undefined,
                          }}
                          className={["relative rounded-[2px] border border-black/10", isUsed ? "cursor-not-allowed" : "cursor-pointer hover:border-black/50"].join(" ")}>
                          {isUsed && (
                            // Diagonal from top-right to bottom-left marking the swatch as taken.
                            <svg className="pointer-events-none absolute inset-0 h-full w-full" viewBox="0 0 10 10" preserveAspectRatio="none" aria-hidden="true">
                              <line x1="10" y1="0" x2="0" y2="10" stroke={contrast} strokeWidth="1" vectorEffect="non-scaling-stroke" />
                            </svg>
                          )}
                        </button>
                      </Tooltip>
                    );
                  })}
                </div>
              </div>
            ))}

            {allowCustomColor && (
              <div className="mt-2 border-t border-slate-200 pt-2">
                <div className="mb-1 text-xs font-semibold text-slate-600">Custom</div>

                <div className="flex items-center gap-1.5">
                  <span
                    aria-hidden="true"
                    style={{ width: swatchSize + 6, height: swatchSize + 6, backgroundColor: customTextIsValid ? customText : "#ffffff" }}
                    className={["shrink-0 rounded-sm border", customTextIsValid ? "border-black/30" : "border-dashed border-slate-400"].join(" ")}
                  />

                  <input
                    type="text"
                    value={customText}
                    placeholder="#rrggbb"
                    aria-label="Custom colour"
                    aria-invalid={customText.length > 0 && !customTextIsValid}
                    onChange={(e) => handleCustomTextChange(e.target.value)}
                    className={[
                      "w-full min-w-0 rounded border px-1.5 py-1 text-xs",
                      customText.length > 0 && !customTextIsValid ? "border-red-500 text-red-700" : "border-slate-300 text-slate-800",
                    ].join(" ")}
                  />
                </div>

                {customText.length > 0 && !customTextIsValid && <div className="mt-1 text-[11px] text-red-600">Not a valid HTML colour.</div>}
              </div>
            )}

            {allowClear && (
              <div className="mt-2 border-t border-slate-200 pt-2">
                <button
                  type="button"
                  aria-pressed={!hasValue}
                  onClick={handleClear}
                  className={[
                    "flex w-full items-center gap-1.5 rounded px-1 py-1 text-xs hover:bg-slate-100",
                    hasValue ? "text-slate-700" : "font-semibold text-slate-900",
                  ].join(" ")}>
                  <span aria-hidden="true" style={{ width: swatchSize, height: swatchSize }} className="shrink-0 rounded-sm border border-dashed border-slate-400 bg-white" />
                  No colour
                </button>
              </div>
            )}
          </div>,
          document.body,
        )}
    </span>
  );
}
