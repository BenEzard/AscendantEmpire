// Small controlled inputs shared by the MapResource Type Manager's pages.
//
// Every numeric value the manager edits is one of two things: a whole number (quantities, SpreadLimits, weights), or a fraction the designer sees
// as a percent (likelihoods, growth, fuzzy factors). These wrap a plain <input> so each page states which it is, and an empty box means "not
// set" (null) rather than 0.
//
// PercentSlider is the exception to that last part: a dragged knob has no empty state, so it reads a null as 0 and always writes a number back.
// A percent only belongs on a slider where null and 0 mean the same thing to the field behind it; where "not set" is a distinct choice (a
// CaptureLikelihood that only applies to the Capture method, say), the field stays a PercentInput.

import { HintIcon } from "@/components/ui";
import { fromPercentInput, fromPercentSlider, fromWholeInput, toPercentInput, toPercentSlider } from "../../tools/mapResourceDraft";

// Full-width for the label/control rows; compact for table cells, where a page may show up to nine columns.
const FULL_CLASS = "w-full rounded-md border border-slate-300 px-2 py-2 text-sm disabled:bg-slate-100 disabled:text-slate-400";
const COMPACT_CLASS = "w-full min-w-[4rem] rounded border border-slate-300 px-1.5 py-1 text-xs disabled:bg-slate-100 disabled:text-slate-400";

type NumberInputProps<T extends number | null> = {
  value: T;
  onChange: (value: T) => void;
  disabled?: boolean;
  compact?: boolean;
  placeholder?: string;
  ariaLabel?: string;
};

/**
 * A whole-number input. With `required`, clearing the box gives 0 rather than null, for the values the server always needs (a layer's quantity
 * range, a scenario amount).
 */
export function WholeNumberInput<T extends number | null>({
  value,
  onChange,
  disabled,
  compact,
  placeholder,
  ariaLabel,
  required,
}: NumberInputProps<T> & { required?: boolean }) {
  return (
    <input
      type="number"
      step={1}
      min={0}
      className={compact ? COMPACT_CLASS : FULL_CLASS}
      value={value ?? ""}
      placeholder={placeholder}
      aria-label={ariaLabel}
      disabled={disabled}
      onChange={(e) => {
        const parsed = fromWholeInput(e.target.value);
        onChange((parsed ?? (required ? 0 : null)) as T);
      }}
    />
  );
}

/** A fraction shown and typed as a percent (0.25 ⇄ 25). With `required`, clearing the box gives 0 rather than null. */
export function PercentInput<T extends number | null>({
  value,
  onChange,
  disabled,
  compact,
  placeholder,
  ariaLabel,
  required,
}: NumberInputProps<T> & { required?: boolean }) {
  return (
    <div className="relative">
      <input
        type="number"
        step={0.1}
        min={0}
        max={100}
        className={`${compact ? COMPACT_CLASS : FULL_CLASS} pr-6`}
        value={toPercentInput(value)}
        placeholder={placeholder}
        aria-label={ariaLabel}
        disabled={disabled}
        onChange={(e) => {
          const parsed = fromPercentInput(e.target.value);
          onChange((parsed ?? (required ? 0 : null)) as T);
        }}
      />
      <span className="pointer-events-none absolute inset-y-0 right-2 flex items-center text-xs text-slate-400" aria-hidden="true">
        %
      </span>
    </div>
  );
}

/**
 * A fraction dragged as a percent, for the full-width label/control rows.
 * <remarks>
 * The knob steps in whole percents; the readout beside it shows the stored value to a tenth, so a value that arrived from the server carrying
 * tenths is reported honestly rather than being silently rounded by the mere act of opening the dialog. Dragging then brings the two together.
 * </remarks>
 */
export function PercentSlider({
  value,
  onChange,
  disabled,
  ariaLabel,
}: {
  value: number | null;
  onChange: (value: number) => void;
  disabled?: boolean;
  ariaLabel?: string;
}) {
  return (
    <div className="flex items-center gap-3">
      <input
        type="range"
        step={1}
        min={0}
        max={100}
        className="w-full disabled:opacity-50"
        value={toPercentSlider(value)}
        aria-label={ariaLabel}
        disabled={disabled}
        onChange={(e) => onChange(fromPercentSlider(Number(e.target.value)))}
      />
      {/* Fixed width so the track does not shift as the number gains or loses a digit while being dragged. */}
      <span className={`w-14 shrink-0 text-right text-sm tabular-nums ${disabled ? "text-slate-400" : "text-slate-700"}`}>
        {toPercentInput(value ?? 0)}%
      </span>
    </div>
  );
}

/** A labelled checkbox, laid out like the Biome Manager's. */
export function CheckboxRow({
  label,
  checked,
  onChange,
  disabled,
  hint,
}: {
  label: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
  disabled?: boolean;
  hint?: string;
}) {
  return (
    <label className="mb-2 flex items-center gap-2 text-sm text-slate-700">
      <input type="checkbox" checked={checked} disabled={disabled} onChange={(e) => onChange(e.target.checked)} />
      {label}
      {/* Same info-icon affordance as FieldRow's hint, so a checkbox row reads identically to the rows around it. */}
      <HintIcon hint={hint} className="align-middle" />
    </label>
  );
}

/** A compact select for table cells. */
export function CompactSelect({
  value,
  onChange,
  options,
  ariaLabel,
  disabled,
}: {
  value: number | string;
  onChange: (value: string) => void;
  options: { value: number | string; label: string }[];
  ariaLabel: string;
  disabled?: boolean;
}) {
  return (
    <select className={COMPACT_CLASS} value={value} aria-label={ariaLabel} disabled={disabled} onChange={(e) => onChange(e.target.value)}>
      {options.map((option) => (
        <option key={option.value} value={option.value}>
          {option.label}
        </option>
      ))}
    </select>
  );
}
