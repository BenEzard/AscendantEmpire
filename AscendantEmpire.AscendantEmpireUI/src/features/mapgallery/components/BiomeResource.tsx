import { Button } from "@/components/ui";
import { BsEraserFill } from "react-icons/bs";
import type { Biome } from "../../../types/map/Biome";
import Tooltip from "@/components/ui/Tooltip";

export interface BiomeResourceProps<K extends keyof Biome = keyof Biome> {
  label: string; /** Label shown on the left (e.g. "Base Fodder") */
  biome: Biome | null; /** The selected biome (or null when nothing selected) */
  field: K; /** Which biome field this edits (e.g. "baseFodderValue") */
  /**
   * Called when the value changes (typically calls your handleBiomeFieldChange).
   * You control typing on the caller side, so this component stays generic.
   */
  onChange: (field: K, value: Biome[K]) => void;
  disabled?: boolean; /** Disable the row (input + clear button). */
  placeholder?: string; /** Placeholder when enabled */
  disabledPlaceholder?: string; /** Placeholder when disabled (defaults to "n/a") */
  inputType?: React.HTMLInputTypeAttribute; /** Input type. Defaults to "number" to match your current usage. */

  /**
   * When true, an empty input becomes `null` instead of `""` / `NaN`.
   * Useful for regenerationRate or nullable base values.
   */
  emptyToNull?: boolean;

  /**
   * Optional custom "can clear?" override.
   * Default: biome exists and current value is not null/undefined.
   */
  canClear?: (biome: Biome) => boolean;

  /**
   * Optional custom "get display value" override.
   * Default: biome[field] ?? ""
   */
  getValue?: (biome: Biome) => string | number;

  /**
   * Optional custom parse. Default:
   * - if inputType === "number": Number(value) (or null if emptyToNull and empty string)
   * - else: raw string
   */
  parseValue?: (raw: string) => Biome[K];

  /** Optional aria label for the clear button (defaults to `Clear ${label}`) */
  clearAriaLabel?: string;

  /** Optional extra class for the outer row container */
  className?: string;
}

export default function BiomeResource<K extends keyof Biome>({
  label,
  biome,
  field,
  onChange,
  disabled = false,
  placeholder = "specify...",
  disabledPlaceholder = "n/a",
  inputType = "number",
  emptyToNull = true,
  canClear,
  getValue,
  parseValue,
  clearAriaLabel,
  className,
}: BiomeResourceProps<K>) {
  const isDisabled = disabled || !biome;

  const value = biome ? (getValue?.(biome) ?? (biome[field] as unknown as string | number | null | undefined) ?? "") : "";

  const computedCanClear = (() => {
    if (!biome) return false;
    if (canClear) return canClear(biome);

    const current = biome[field] as unknown as any;
    return current !== null && current !== undefined;
  })();

  const handleInputChange = (raw: string) => {
    if (!biome) return;

    if (parseValue) {
      onChange(field, parseValue(raw));
      return;
    }

    // Default parse behavior
    if (inputType === "number") {
      if (emptyToNull && raw === "") {
        onChange(field, null as unknown as Biome[K]);
        return;
      }

      onChange(field, Number(raw) as unknown as Biome[K]);
      return;
    }

    // Non-number inputs
    onChange(field, raw as unknown as Biome[K]);
  };

  const handleClear = () => {
    if (!biome) return;
    onChange(field, null as unknown as Biome[K]);
  };

  return (
    <div className={["grid grid-cols-12 items-center gap-2 mb-2", className].filter(Boolean).join(" ")}>
      <label className="col-span-6 text-sm text-slate-700">{label}</label>

      <div className="col-span-6 flex items-center gap-2">
        <input
          className="w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
          type={inputType}
          disabled={isDisabled}
          placeholder={isDisabled ? disabledPlaceholder : placeholder}
          value={value as any}
          onChange={(e) => handleInputChange(e.target.value)}
        />

        <Tooltip content="Clear">
          <Button
            variant="outline-secondary"
            className="px-2 py-2 shrink-0"
            aria-label={clearAriaLabel ?? `Clear ${label}`}
            disabled={isDisabled || !computedCanClear}
            onClick={handleClear}>
            <BsEraserFill aria-hidden="true" />
          </Button>
        </Tooltip>
      </div>
    </div>
  );
}
