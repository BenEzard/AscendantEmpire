// Shared presentational field primitives for the Component Setup dialog sections.
//
// These were originally declared locally inside ComponentSetupDialog. They're lifted here so every
// section (General, the per-type Properties editors, and the upcoming Slots/Tags/Supersession
// sections) renders the same two-column label/control layout and input styling without duplicating it.

import type { ReactNode } from "react";

// The shared input styling used by the dialog's text/number/select controls. Exported so the other
// editors (e.g. PillKeyValueEditor, ScaledNumberField) can reuse the exact same look.
export const inputClass = "w-full rounded-md border border-slate-300 px-2 py-2 text-sm";
// The matching error-state styling lives in ./fieldStyles — see the note at the top of that file.

/**
 * A label/control row matching the two-column layout used by the other manager dialogs.
 * The optional hint renders as small muted text after the label (e.g. "(1.0 = base)").
 * `align` controls how the label sits against the control: "center" (default) vertically centres it,
 * while "top" pins it to the control's first line — better when the control is a tall, multi-line input.
 * `className` is appended to the outer row so a caller can tune spacing (e.g. a negative top margin to
 * tighten the gap to the previous row) without changing the default layout for every other row.
 * `error`, when set, renders a small red validation message directly beneath the control.
 */
export function FieldRow({
  label,
  hint,
  align = "center",
  className = "",
  error,
  children,
}: {
  label: string;
  hint?: string;
  align?: "center" | "top";
  className?: string;
  error?: string;
  children: ReactNode;
}) {
  const isTop = align === "top";

  return (
    <div className={`mb-2 grid grid-cols-12 gap-2 ${isTop ? "items-start" : "items-center"} ${className}`}>
      {/* When top-aligned, nudge the label down by the control's vertical padding (py-2) so its text
          lines up with the control's first line rather than the control's border. */}
      <label className={`col-span-5 text-sm text-slate-700 ${isTop ? "pt-2" : ""}`}>
        {label}
        {hint ? <span className="ml-1 text-xs text-slate-400">{hint}</span> : null}
      </label>
      <div className="col-span-7">
        {children}
        {/* Inline per-field validation message shown after a failed Save attempt. */}
        {error ? <p className="mt-1 text-xs text-red-600">{error}</p> : null}
      </div>
    </div>
  );
}

/** A plain numeric input bound to a number value (no fixed-point scaling — see ScaledNumberField for that). */
export function NumberField({ value, onChange, step }: { value: number; onChange: (next: number) => void; step?: number }) {
  return (
    <input
      className={inputClass}
      type="number"
      step={step}
      // Guard against NaN/Infinity sneaking into the controlled input.
      value={Number.isFinite(value) ? value : 0}
      onChange={(e) => onChange(Number(e.target.value))}
    />
  );
}
