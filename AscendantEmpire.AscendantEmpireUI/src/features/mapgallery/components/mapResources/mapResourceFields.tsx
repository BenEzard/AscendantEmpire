// Small controlled inputs shared by the MapResource Type Manager's pages.
//
// Every numeric value the manager edits is one of two things: a whole number (quantities, SpreadLimits, weights), or a fraction the designer sees
// as a percent (likelihoods, growth, fuzzy factors). These wrap a plain <input> so each page states which it is, and an empty box means "not
// set" (null) rather than 0.

import { fromPercentInput, fromWholeInput, toPercentInput } from "../../tools/mapResourceDraft";

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
      {hint ? <span className="text-xs text-slate-400">{hint}</span> : null}
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
