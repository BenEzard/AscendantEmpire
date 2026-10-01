// HintIcon — the small info affordance that sits after a field's label and shows the field's hint on hover.
//
// Field hints used to render as muted text inline after the label, which competed with the label itself for
// attention and pushed the label out of alignment with the rows around it. The icon keeps the row clean and
// defers the explanation until it's asked for. Shared rather than inlined into FieldRow because the manager
// dialogs' checkbox rows carry the same kind of hint and must look identical.

import { FaCircleInfo } from "react-icons/fa6";
import Tooltip from "./Tooltip";

/**
 * Hints are authored wrapped in parentheses — e.g. "(1.0 = base)" — from when they rendered as inline text
 * after the label. The tooltip bubble supplies its own framing, so strip one wrapping pair here rather than
 * making every call site rewrite its string. A hint without them is shown as authored.
 */
function toHintText(hint: string): string {
  const trimmed = hint.trim();

  if (trimmed.startsWith("(") && trimmed.endsWith(")")) {
    return trimmed.slice(1, -1).trim();
  }

  return trimmed;
}

/**
 * Renders nothing when there's no hint, so a caller can hand over an optional hint without guarding first.
 * `className` is applied to the tooltip's trigger wrapper, for the caller's own spacing against the label.
 */
export default function HintIcon({ hint, className = "" }: { hint?: string; className?: string }) {
  if (!hint) {
    return null;
  }

  const text = toHintText(hint);

  return (
    <Tooltip content={text} className={className}>
      {/* The icon carries no text of its own, so the wrapper is the accessible name for screen readers
          while the icon itself is hidden from them. */}
      <span role="img" aria-label={text} className="text-slate-400 transition-colors hover:text-slate-600">
        <FaCircleInfo size={12} aria-hidden />
      </span>
    </Tooltip>
  );
}
