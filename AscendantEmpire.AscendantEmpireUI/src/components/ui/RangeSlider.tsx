// RangeSlider — a horizontal slider with two knobs (a Min and a Max) that together select a range
// within a track. The Min knob and a small "Min" caption ride above the line; the Max knob and a
// "Max" caption ride below it, so the two are always visually distinct even when their values meet.
//
// Controlled component: the caller owns the two values (minValue / maxValue) and the track bounds
// (trackMin / trackMax) plus the step. Every knob move emits the whole { min, max } pair via onChange.
// The pair is always kept ordered — the Min knob can't be dragged past the Max knob, and vice-versa.

import { useId, useRef, type KeyboardEvent, type PointerEvent } from "react";

export interface RangeSliderProps {
  /** Label shown to the left of the slider. Defaults to "Range". */
  label?: string;
  /** The Min knob's value (the low end of the selected range). Defaults to 0. */
  minValue?: number;
  /** The Max knob's value (the high end of the selected range). Defaults to 0. */
  maxValue?: number;
  /** Lower bound of the track itself. Defaults to 0. */
  trackMin?: number;
  /** Upper bound of the track itself. Defaults to 10. */
  trackMax?: number;
  /** Granularity the knobs snap to. Defaults to 1. */
  step?: number;
  /** Emits the updated pair whenever either knob moves. */
  onChange: (next: { min: number; max: number }) => void;
  /** When true the knobs can't be moved and the control is greyed out. */
  disabled?: boolean;
}

// Distance (px) from a knob's centre to its caption — roughly half the knob's fixed min-width plus a
// small gap, so the "Min"/"Max" captions clear the knob instead of overlapping it.
const CAPTION_OFFSET = 20;

// Horizontal inset (px) at each end of the track. It has to be wide enough that, at the extremes, the
// caption sitting *outside* the knob (Min to the left at trackMin, Max to the right at trackMax) still
// fits inside the component rather than spilling over the edge and forcing a horizontal scrollbar —
// hence CAPTION_OFFSET plus roughly the caption's own width.
const TRACK_INSET = CAPTION_OFFSET + 26;

// Which knob a thumb interaction is driving.
type Thumb = "min" | "max";

export default function RangeSlider({
  label = "Range",
  minValue = 0,
  maxValue = 0,
  trackMin = 0,
  trackMax = 10,
  step = 1,
  onChange,
  disabled = false,
}: RangeSliderProps) {
  // The track element, measured live during a drag to convert a pointer X into a value.
  const trackRef = useRef<HTMLDivElement>(null);
  const labelId = useId();

  // Guard against a divide-by-zero when the bounds are equal (degenerate, but possible via props).
  const span = Math.max(trackMax - trackMin, 1);

  // Snap a raw value onto the step grid (measured from trackMin) and clamp it into the track bounds.
  const snap = (raw: number) => {
    const stepped = trackMin + Math.round((raw - trackMin) / step) * step;
    return Math.min(trackMax, Math.max(trackMin, stepped));
  };

  // A knob's horizontal centre as a CSS length expression (without the calc() wrapper, so it can be
  // embedded inside other calc() expressions — e.g. when anchoring a caption beside a knob). Uses % so
  // the slider can be any width (responsive) rather than a fixed pixel groove.
  const centreExpr = (value: number) => {
    // Clamp the fraction so an out-of-bounds value (e.g. a stale prop) can't push a knob off the track.
    const fraction = Math.min(1, Math.max(0, (value - trackMin) / span));
    return `${TRACK_INSET}px + ${fraction} * (100% - ${TRACK_INSET * 2}px)`;
  };
  const offsetFor = (value: number) => `calc(${centreExpr(value)})`;

  // Emit the moved knob, keeping the pair ordered: Min can't exceed Max and Max can't fall below Min.
  const commit = (thumb: Thumb, value: number) => {
    if (thumb === "min") {
      onChange({ min: Math.min(value, maxValue), max: maxValue });
    } else {
      onChange({ min: minValue, max: Math.max(value, minValue) });
    }
  };

  // Turn a pointer's clientX into a snapped value using the track's current on-screen geometry.
  const valueFromClientX = (clientX: number) => {
    const track = trackRef.current;
    if (!track) {
      return trackMin;
    }
    const rect = track.getBoundingClientRect();
    const usable = Math.max(rect.width - TRACK_INSET * 2, 1);
    const x = Math.min(Math.max(clientX - rect.left - TRACK_INSET, 0), usable);
    return snap(trackMin + (x / usable) * span);
  };

  // Begin a drag: capture the pointer on the knob so we keep receiving move/up events even if the
  // cursor leaves the knob. Capture is released automatically on pointerup (and dropped by the browser
  // if the knob unmounts mid-drag), so there are no document listeners to clean up.
  const handlePointerDown = () => (event: PointerEvent<HTMLButtonElement>) => {
    if (disabled) {
      return;
    }
    event.preventDefault();
    event.currentTarget.setPointerCapture(event.pointerId);
  };

  const handlePointerMove = (thumb: Thumb) => (event: PointerEvent<HTMLButtonElement>) => {
    // Only react while this knob holds the pointer capture (i.e. an active drag).
    if (!event.currentTarget.hasPointerCapture(event.pointerId)) {
      return;
    }
    commit(thumb, valueFromClientX(event.clientX));
  };

  const handlePointerUp = (event: PointerEvent<HTMLButtonElement>) => {
    if (event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId);
    }
  };

  // Keyboard support: arrows nudge by one step, Home/End jump to the track bounds.
  const handleKeyDown = (thumb: Thumb) => (event: KeyboardEvent<HTMLButtonElement>) => {
    if (disabled) {
      return;
    }
    const current = thumb === "min" ? minValue : maxValue;
    let next = current;
    if (event.key === "ArrowRight" || event.key === "ArrowUp") {
      next = snap(current + step);
    } else if (event.key === "ArrowLeft" || event.key === "ArrowDown") {
      next = snap(current - step);
    } else if (event.key === "Home") {
      next = trackMin;
    } else if (event.key === "End") {
      next = trackMax;
    } else {
      return;
    }
    event.preventDefault();
    commit(thumb, next);
  };

  // Shared knob styling; the per-thumb bits (position, colours when disabled) are layered on top.
  const knobClass = [
    "absolute z-10 min-w-[28px] -translate-x-1/2 -translate-y-1/2 rounded px-1.5 py-0.5 text-center text-xs font-semibold text-white outline-none",
    "focus-visible:ring-2 focus-visible:ring-slate-400",
    disabled ? "cursor-not-allowed bg-slate-300" : "cursor-grab bg-slate-500 active:cursor-grabbing",
  ].join(" ");

  return (
    <div className="mb-2 grid grid-cols-12 items-center gap-2">
      <span id={labelId} className="col-span-5 text-sm text-slate-700">
        {label}
      </span>

      <div className="col-span-7">
        {/* The track band. Tall enough to stack: Min caption / Min knob / line / Max knob / Max caption.
            overflow-hidden is a safety net so a knob/caption near an edge can never push the page wider
            (no horizontal scrollbar); TRACK_INSET is sized so nothing actually needs clipping. */}
        <div ref={trackRef} className="relative h-16 select-none overflow-hidden" aria-labelledby={labelId}>
          {/* Base line, inset on both ends so it aligns with the knobs' travel range. */}
          <div className="absolute top-1/2 h-0.5 -translate-y-1/2 rounded-full bg-slate-300" style={{ left: TRACK_INSET, right: TRACK_INSET }} />

          {/* Filled segment between the two knobs. */}
          <div
            className="absolute top-1/2 h-0.5 -translate-y-1/2 rounded-full bg-slate-600"
            style={{ left: offsetFor(minValue), right: `calc(100% - (${offsetFor(maxValue)}))` }}
          />

          {/* "Min" caption — to the left of the Min knob, vertically aligned with it (above the line). */}
          <span
            className="pointer-events-none absolute -translate-y-1/2 text-right text-[10px] font-medium text-slate-400"
            style={{ right: `calc(100% - (${centreExpr(minValue)}) + ${CAPTION_OFFSET}px)`, top: 22 }}>
            Min
          </span>

          {/* Min knob — sits above the line. */}
          <button
            type="button"
            role="slider"
            aria-label={`${label} minimum`}
            aria-valuemin={trackMin}
            aria-valuemax={maxValue}
            aria-valuenow={minValue}
            tabIndex={disabled ? -1 : 0}
            disabled={disabled}
            className={knobClass}
            style={{ left: offsetFor(minValue), top: 22 }}
            onPointerDown={handlePointerDown()}
            onPointerMove={handlePointerMove("min")}
            onPointerUp={handlePointerUp}
            onKeyDown={handleKeyDown("min")}>
            {minValue}
          </button>

          {/* Max knob — sits below the line. */}
          <button
            type="button"
            role="slider"
            aria-label={`${label} maximum`}
            aria-valuemin={minValue}
            aria-valuemax={trackMax}
            aria-valuenow={maxValue}
            tabIndex={disabled ? -1 : 0}
            disabled={disabled}
            className={knobClass}
            style={{ left: offsetFor(maxValue), top: 42 }}
            onPointerDown={handlePointerDown()}
            onPointerMove={handlePointerMove("max")}
            onPointerUp={handlePointerUp}
            onKeyDown={handleKeyDown("max")}>
            {maxValue}
          </button>

          {/* "Max" caption — to the right of the Max knob, vertically aligned with it (below the line). */}
          <span
            className="pointer-events-none absolute -translate-y-1/2 text-left text-[10px] font-medium text-slate-400"
            style={{ left: `calc((${centreExpr(maxValue)}) + ${CAPTION_OFFSET}px)`, top: 42 }}>
            Max
          </span>
        </div>
      </div>
    </div>
  );
}
