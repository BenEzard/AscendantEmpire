// Field primitives local to the Component Setup dialog sections.
//
// FieldRow, inputClass and errorClass have moved to the shared UI library (@/components/ui/FieldRow
// and @/components/ui/fieldStyles) now that player-facing dialogs want the same row; importing a dev
// tool's internals from those would have been the wrong dependency direction. This module keeps only
// what nothing outside the setup tool uses.

import { inputClass } from "@/components/ui/FieldRow";

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
