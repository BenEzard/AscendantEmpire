// TagsSection — the dialog's "Tags" page. A component references buff/classification tags by their
// code (the tag definitions themselves live in the global tag catalog, edited elsewhere). So here we
// just manage the component's set of tag *codes* as a list of chips.
//
// Controlled editor: the committed codes come from props; only the in-progress entry text is local.

import { useState } from "react";
import { HiXMark } from "react-icons/hi2";

import { inputClass } from "../componentSetupFields";

interface TagsSectionProps {
  tagCodes: string[];
  onChange: (next: string[]) => void;
}

// Normalise a typed tag to the same code convention used elsewhere: trimmed, lowercase, spaces -> "_".
function toTagCode(raw: string): string {
  return raw.trim().toLowerCase().replace(/\s+/g, "_");
}

export default function TagsSection({ tagCodes, onChange }: TagsSectionProps) {
  // The text currently being typed in the entry box (not yet committed to a chip).
  const [entry, setEntry] = useState("");

  const addEntry = () => {
    const code = toTagCode(entry);
    // Ignore blanks and duplicates; otherwise append and clear the entry box.
    if (code === "" || tagCodes.includes(code)) {
      setEntry("");
      return;
    }

    onChange([...tagCodes, code]);
    setEntry("");
  };

  const removeCode = (code: string) => {
    onChange(tagCodes.filter((existing) => existing !== code));
  };

  return (
    <div className="w-full max-w-xl">
      <p className="mb-2 text-xs text-slate-400">
        Tag codes attached to this component. Each must resolve to a tag in the catalog when the component is saved.
      </p>

      <div className="mb-3 flex items-center gap-2">
        <input
          className={inputClass}
          value={entry}
          placeholder="tag code, e.g. amphibious"
          // Enter commits the current entry, matching the "+" button.
          onKeyDown={(e) => {
            if (e.key === "Enter") {
              e.preventDefault();
              addEntry();
            }
          }}
          onChange={(e) => setEntry(e.target.value)}
        />
        <button
          type="button"
          className="rounded-md border border-slate-300 px-3 py-2 text-sm text-slate-700 hover:bg-slate-50"
          onClick={addEntry}>
          Add
        </button>
      </div>

      {tagCodes.length === 0 ? (
        <p className="text-sm text-slate-500">No tags yet.</p>
      ) : (
        <div className="flex flex-wrap gap-2">
          {tagCodes.map((code) => (
            <span key={code} className="inline-flex items-center gap-1 rounded-full bg-slate-100 px-2.5 py-1 text-sm text-slate-700">
              {code}
              <button
                type="button"
                className="text-slate-400 hover:text-[var(--bs-danger)]"
                aria-label={`Remove ${code}`}
                onClick={() => removeCode(code)}>
                <HiXMark size={14} aria-hidden />
              </button>
            </span>
          ))}
        </div>
      )}
    </div>
  );
}
