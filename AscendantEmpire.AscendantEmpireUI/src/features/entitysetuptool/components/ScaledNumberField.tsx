// ScaledNumberField — a numeric input that edits a raw Scale-1,000 fixed-point value while showing the
// user a friendly decimal (1,000 raw <-> 1.0 shown). Shared by ComponentSetupDialog and the pill editor
// so both convert at the same edit boundary.

// The Scale-1,000 conversion helpers now live with the canonical model (features/unitdesign/types);
// re-exported here so existing importers (PillKeyValueEditor) keep resolving them from this module.
import { rawToScaled, scaledToRaw } from "@/features/unitdesign/types/UnitComponent";
export { rawToScaled, scaledToRaw };

// Local copy of the dialog's input styling so this stays self-contained.
const inputClass = "w-full rounded-md border border-slate-300 px-2 py-2 text-sm";

/**
 * A numeric input that edits a raw Scale-1,000 value while showing the user a decimal. When the field
 * is nullable, an empty box maps to null (no contribution); typing a number stores the raw equivalent.
 */
export default function ScaledNumberField({
  rawValue,
  onChange,
  nullable,
}: {
  rawValue: number | null;
  onChange: (next: number | null) => void;
  nullable?: boolean;
}) {
  // Show the decimal form of the stored raw value; blank when null.
  const display = rawValue == null ? "" : String(rawToScaled(rawValue));

  const handleChange = (raw: string) => {
    // An empty box means "no value" for nullable fields; otherwise it falls back to 0.
    if (raw.trim() === "") {
      onChange(nullable ? null : 0);
      return;
    }

    onChange(scaledToRaw(Number(raw)));
  };

  return (
    <input
      className={inputClass}
      type="number"
      step="0.001"
      value={display}
      placeholder={nullable ? "none" : undefined}
      onChange={(e) => handleChange(e.target.value)}
    />
  );
}
