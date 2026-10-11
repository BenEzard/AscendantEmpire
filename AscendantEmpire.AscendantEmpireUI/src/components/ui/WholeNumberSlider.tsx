// WholeNumberSlider — a whole number dragged on a slider, for the full-width label/control rows, with the chosen value pinned in a readout beside
// the track.
//
// Controlled: the caller owns the value and the bounds. Shared rather than per-feature, because every "small, known range" numeric setting wants the
// same control — a MapResource's Survival Rounds, a new game's player count.

export interface WholeNumberSliderProps {
  /** The current value. A null reads as `min`, since a dragged knob has no empty state. */
  value: number | null;
  onChange: (value: number) => void;
  /** Lower bound of the track. The knob cannot leave it. */
  min: number;
  /** Upper bound of the track. The knob cannot leave it. */
  max: number;
  /** When true the knob can't be moved and the control is greyed out. */
  disabled?: boolean;
  ariaLabel?: string;
  /** Appended to the readout, e.g. "Rounds". Singular/plural is the caller's business; omit it for a bare number. */
  unitLabel?: string;
}

/**
 * A whole number dragged on a slider, for the full-width label/control rows.
 *
 * <remarks>
 * A dragged knob has no empty state: this reads a null as `min` and always writes a number back. So it belongs only on a field that is always set —
 * one the server insists on, or one a default supplies — and a field where "not set" is a distinct choice wants a number input instead.
 *
 * The caller gives the bounds, because they are the field's own rule rather than anything general: the knob cannot leave them, which is the point of
 * choosing a slider over a box for a value with a small, known range. It also means no invalid value can be produced, so a caller using this needs
 * no range validation or error message behind it.
 * </remarks>
 */
export default function WholeNumberSlider({ value, onChange, min, max, disabled, ariaLabel, unitLabel }: WholeNumberSliderProps) {
  return (
    <div className="flex items-center gap-3">
      <input
        type="range"
        step={1}
        min={min}
        max={max}
        className="w-full disabled:opacity-50"
        value={value ?? min}
        aria-label={ariaLabel}
        disabled={disabled}
        onChange={(e) => onChange(Number(e.target.value))}
      />
      {/* Fixed width so the track does not shift as the number gains or loses a digit while being dragged. Wide enough for the longest unit label
          its callers use. */}
      <span className={`w-20 shrink-0 text-right text-sm tabular-nums ${disabled ? "text-slate-400" : "text-slate-700"}`}>
        {value ?? min}
        {unitLabel ? ` ${unitLabel}` : ""}
      </span>
    </div>
  );
}
