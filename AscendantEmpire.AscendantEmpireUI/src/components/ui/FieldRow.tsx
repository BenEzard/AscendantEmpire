// FieldRow — the shared label/control row used by the setup and manager dialogs.
//
// This started life inside ComponentSetupDialog, then moved to the entitysetuptool feature so its
// sections could share it. It lives here now because player-facing dialogs want the same row, and
// reaching into a dev tool's internals for a layout primitive is the wrong dependency direction.
// componentSetupFields.tsx re-exports it, so the existing call sites did not have to change.

import type { ReactNode } from "react";
import HintIcon from "./HintIcon";

// The shared input styling used by the dialogs' text/number/select controls. Exported so the other
// editors (e.g. PillKeyValueEditor, ScaledNumberField) can reuse the exact same look.
export const inputClass = "w-full rounded-md border border-slate-300 px-2 py-2 text-sm";
// The matching error-state styling lives in ./fieldStyles — see the note at the top of that file.

/**
 * A label/control row matching the two-column layout used by the manager dialogs.
 * The optional hint renders as a small info icon after the label, with the hint text as its tooltip
 * (e.g. "(1.0 = base)" becomes a bubble reading "1.0 = base") — see HintIcon.
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
        <HintIcon hint={hint} className="ml-1 align-middle" />
      </label>
      <div className="col-span-7">
        {children}
        {/* Inline per-field validation message shown after a failed Save attempt. */}
        {error ? <p className="mt-1 text-xs text-red-600">{error}</p> : null}
      </div>
    </div>
  );
}
