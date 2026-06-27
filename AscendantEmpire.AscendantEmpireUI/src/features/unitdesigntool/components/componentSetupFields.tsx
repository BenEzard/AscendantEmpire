// Shared presentational field primitives for the Component Setup dialog sections.
//
// These were originally declared locally inside ComponentSetupDialog. They're lifted here so every
// section (General, the per-type Properties editors, and the upcoming Slots/Tags/Supersession
// sections) renders the same two-column label/control layout and input styling without duplicating it.

import type { ReactNode } from "react";

// The shared input styling used by the dialog's text/number/select controls. Exported so the other
// editors (e.g. PillKeyValueEditor, ScaledNumberField) can reuse the exact same look.
export const inputClass = "w-full rounded-md border border-slate-300 px-2 py-2 text-sm";

/**
 * A label/control row matching the two-column layout used by the other manager dialogs.
 * The optional hint renders as small muted text after the label (e.g. "(1.0 = base)").
 */
export function FieldRow({ label, hint, children }: { label: string; hint?: string; children: ReactNode }) {
  return (
    <div className="mb-2 grid grid-cols-12 items-center gap-2">
      <label className="col-span-5 text-sm text-slate-700">
        {label}
        {hint ? <span className="ml-1 text-xs text-slate-400">{hint}</span> : null}
      </label>
      <div className="col-span-7">{children}</div>
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
