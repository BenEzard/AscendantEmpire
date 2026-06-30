// CodeField — a slug/code text input that derives its value from a related "source" field (e.g. a
// display name) while still letting the user take manual control.
//
// Behaviour:
//   • While untouched, the code auto-tracks `source`: lowercased, with every non-alphanumeric character
//     swapped for a hyphen and leading/trailing hyphens trimmed (see toCodeSlug).
//   • Typing directly in the field is a manual override. The input is still sanitised live (lowercase +
//     hyphens, but NOT trimmed, so separators stay typeable), it stops tracking `source`, and the
//     pencil affordance turns orange to flag that the code was hand-edited.
//   • Clicking the pencil discards the manual value and re-derives the code from the current `source`.

import { useEffect, useRef, useState } from "react";
import { FaPencil } from "react-icons/fa6";

import Tooltip from "./Tooltip";

// The shared text-input styling (kept in step with the dialogs' `inputClass`) so a CodeField's input
// looks identical to a plain text field next to it.
const DEFAULT_INPUT_CLASS = "w-full rounded-md border border-slate-300 px-2 py-2 text-sm";

/**
 * Lowercase a string and replace each non-alphanumeric character with a single hyphen (one per
 * character — runs are NOT collapsed). Does not trim, so it is safe to apply to live manual typing.
 */
export function toCodeChars(value: string): string {
  return value.toLowerCase().replace(/[^a-z0-9]/g, "-");
}

/**
 * The full code algorithm: {@link toCodeChars} plus trimming of leading/trailing hyphens. Used to
 * auto-derive the code from the source field and when resetting via the pencil.
 */
export function toCodeSlug(source: string): string {
  return toCodeChars(source).replace(/^-+|-+$/g, "");
}

export interface CodeFieldProps {
  /** The current code value (controlled). */
  value: string;
  /** Emits the new code: on sanitised manual input, on source-derived updates, and on pencil reset. */
  onChange: (next: string) => void;
  /** The related field the code derives from while untouched (e.g. the display name). */
  source: string;
  placeholder?: string;
  /** Overrides the default text-input styling. */
  className?: string;
  id?: string;
}

export default function CodeField({ value, onChange, source, placeholder, className, id }: CodeFieldProps) {
  // Whether the user has hand-edited the code. Once true we stop auto-tracking `source` (until reset via
  // the pencil). Seeded from the initial props so an existing custom code opens already flagged manual.
  const [isManuallyEdited, setIsManuallyEdited] = useState(() => value !== "" && value !== toCodeSlug(source));

  // Latest value/onChange, read inside the source-sync effect without making it re-run on every render.
  const valueRef = useRef(value);
  valueRef.current = value;
  const onChangeRef = useRef(onChange);
  onChangeRef.current = onChange;

  // Keep the code in step with the source field until the user takes manual control. Runs only when the
  // source (or the manual flag) changes; on mount the derived value already matches, so nothing fires.
  useEffect(() => {
    if (isManuallyEdited) {
      return;
    }

    const derived = toCodeSlug(source);
    if (derived !== valueRef.current) {
      onChangeRef.current(derived);
    }
  }, [source, isManuallyEdited]);

  // Manual typing: sanitise live (lowercase + hyphens, no trim) and flag the code as hand-edited.
  const handleManualInput = (raw: string) => {
    setIsManuallyEdited(true);
    onChange(toCodeChars(raw));
  };

  // Pencil: discard the manual value and go back to the source-derived code.
  const resetToDerived = () => {
    setIsManuallyEdited(false);
    onChange(toCodeSlug(source));
  };

  return (
    <div className="flex items-center gap-2">
      <input
        id={id}
        className={`${className ?? DEFAULT_INPUT_CLASS} min-w-0 flex-1`}
        value={value}
        placeholder={placeholder}
        onChange={(e) => handleManualInput(e.target.value)}
      />

      {/* Grey by default; orange once the code has been hand-edited. Clicking resets to the derived code. */}
      <Tooltip content="Code has been manually edited">
        <button
          type="button"
          aria-label="Code has been manually edited"
          className={`shrink-0 transition-colors ${isManuallyEdited ? "text-orange-500 hover:text-orange-600" : "text-slate-400 hover:text-slate-600"}`}
          onClick={resetToDerived}>
          <FaPencil size={14} aria-hidden />
        </button>
      </Tooltip>
    </div>
  );
}
