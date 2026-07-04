// PillKeyValueEditor — a compact editor for a sparse "one entry per key" list (weapon Attack, armour
// Resistance, weapon Target Domains). Instead of one table row per entry, it shows a single fixed entry
// row (a key dropdown + a value input) above a horizontal, wrapping area of "pills" — one pill per
// committed entry.
//
// Interaction:
//   • Nothing selected → the entry row composes a NEW entry; the dropdown offers only unused keys, and
//     the "+" button commits it as a pill.
//   • Click a pill → it becomes selected (highlighted) and loads into the entry row, which now edits
//     that pill live (changes apply immediately).
//   • The "🗑" button deletes the selected pill; "+" while a pill is selected starts a fresh new entry.
//
// Like the old table editor this enforces one entry per key (the dropdown only ever offers keys not in
// use elsewhere) and works over the shared KeyValueRow<TKey> model, so the dialog's row state and commit
// translation are unchanged.

import { useState } from "react";
import type { IconType } from "react-icons";
import { HiPlus, HiTrash } from "react-icons/hi2";

import Tooltip from "@/components/ui/Tooltip";

import { rawToScaled, type KeyValueRow } from "@/features/unitdesign/types/UnitComponent";
import { inputClass } from "./componentSetupFields";
import ScaledNumberField from "./ScaledNumberField";

interface PillKeyValueEditorProps<TKey extends string> {
  /** The committed entries (the pills). */
  rows: KeyValueRow<TKey>[];
  onChange: (next: KeyValueRow<TKey>[]) => void;
  /** Every key this editor could hold, in display order. */
  allKeys: readonly TKey[];
  /** Human-readable label for each key (used in the dropdown and as the pill tooltip). */
  keyLabels: Record<TKey, string>;
  /** Optional glyph per key. When absent for a key, its pill falls back to the text label. */
  keyIcons?: Partial<Record<TKey, IconType>>;
  /** Column header above the key dropdown (e.g. "Damage Type"). Also names the add action. */
  keyHeader: string;
  /** Column header above the value input (e.g. "Damage Value"). */
  valueHeader: string;
  /** Value (raw Scale-1,000) given to a freshly composed entry. */
  defaultValueRaw: number;
}

export default function PillKeyValueEditor<TKey extends string>({
  rows,
  onChange,
  allKeys,
  keyLabels,
  keyIcons,
  keyHeader,
  valueHeader,
  defaultValueRaw,
}: PillKeyValueEditorProps<TKey>) {
  // Which pill is currently being edited (null = composing a new entry).
  const [selectedIndex, setSelectedIndex] = useState<number | null>(null);
  // The new-entry draft (only consulted when nothing is selected).
  const [draftKey, setDraftKey] = useState<TKey | null>(null);
  const [draftValue, setDraftValue] = useState<number>(defaultValueRaw);

  // Keys already taken by an existing pill, and the first key still free (null when every key is used).
  const usedKeys = new Set(rows.map((row) => row.key));
  const firstUnused = allKeys.find((key) => !usedKeys.has(key)) ?? null;

  // Clamp a stale selection (e.g. left over from a previous open) back into range.
  const sel = selectedIndex != null && selectedIndex < rows.length ? selectedIndex : null;
  const editing = sel != null;

  // The key/value the entry row currently shows. While editing it mirrors the selected pill; otherwise
  // it's the draft, falling back to the first unused key if the draft key is missing/now-taken.
  const currentKey: TKey | null = editing
    ? rows[sel].key
    : draftKey != null && !usedKeys.has(draftKey)
      ? draftKey
      : firstUnused;
  const currentValue = editing ? rows[sel].value : draftValue;

  // When composing a new entry and every key is already used, the entry row has nothing to offer.
  const entryInert = !editing && currentKey == null;

  // Dropdown options: while editing, this pill's own key plus any free key; otherwise just the free keys.
  const keyOptions = editing
    ? allKeys.filter((key) => key === currentKey || !usedKeys.has(key))
    : allKeys.filter((key) => !usedKeys.has(key));

  // Replace one row with a patched copy (used for live edits to the selected pill).
  const updateRow = (index: number, patch: Partial<KeyValueRow<TKey>>) => {
    onChange(rows.map((row, i) => (i === index ? { ...row, ...patch } : row)));
  };

  const handleKeyChange = (nextKey: TKey) => {
    if (editing) {
      updateRow(sel, { key: nextKey });
    } else {
      setDraftKey(nextKey);
    }
  };

  const handleValueChange = (rawValue: number) => {
    if (editing) {
      updateRow(sel, { value: rawValue });
    } else {
      setDraftValue(rawValue);
    }
  };

  // "+" — commit the draft as a new pill, or (while editing) drop back to composing a fresh entry.
  const handleAdd = () => {
    if (editing) {
      setSelectedIndex(null);
      setDraftKey(firstUnused);
      setDraftValue(defaultValueRaw);
      return;
    }

    if (currentKey == null) {
      return;
    }

    onChange([...rows, { key: currentKey, value: draftValue }]);
    // Point the draft at the next key that's still free after this addition.
    setDraftKey(allKeys.find((key) => key !== currentKey && !usedKeys.has(key)) ?? null);
    setDraftValue(defaultValueRaw);
  };

  // "🗑" — delete the selected pill and return to composing (its key is now free again).
  const handleRemove = () => {
    if (sel == null) {
      return;
    }

    const freedKey = rows[sel].key;
    onChange(rows.filter((_, i) => i !== sel));
    setSelectedIndex(null);
    setDraftKey(freedKey);
    setDraftValue(defaultValueRaw);
  };

  // Click a pill to edit it; click the already-selected pill to deselect (back to composing).
  const togglePill = (index: number) => {
    if (sel === index) {
      setSelectedIndex(null);
      setDraftKey(null);
      setDraftValue(defaultValueRaw);
    } else {
      setSelectedIndex(index);
    }
  };

  return (
    <div className="rounded-md border border-slate-200">
      {/* Entry row: a header line, then the key dropdown + value input + add/remove buttons. */}
      <div className="border-b border-slate-200 p-2">
        <div className="grid grid-cols-12 gap-2 px-1 pb-1 text-xs font-medium text-slate-500">
          <div className="col-span-5">{keyHeader}</div>
          <div className="col-span-4">{valueHeader}</div>
          <div className="col-span-3" />
        </div>

        <div className="grid grid-cols-12 items-center gap-2">
          <div className="col-span-5">
            <select
              className={inputClass}
              value={currentKey ?? ""}
              disabled={entryInert}
              onChange={(e) => handleKeyChange(e.target.value as TKey)}
            >
              {entryInert ? <option value="">— all added —</option> : null}
              {keyOptions.map((key) => (
                <option key={key} value={key}>
                  {keyLabels[key]}
                </option>
              ))}
            </select>
          </div>

          <div className="col-span-4">
            <ScaledNumberField rawValue={currentValue} nullable={false} onChange={(next) => handleValueChange(next ?? 0)} />
          </div>

          <div className="col-span-3 flex items-center justify-end gap-3">
            <Tooltip content={editing ? `New ${keyHeader}` : `Add ${keyHeader}`}>
              {/* Plain icon controls (no border/padding) so they read as subtle actions, not buttons. */}
              <button
                type="button"
                className="text-slate-500 hover:text-[var(--bs-primary)] disabled:opacity-30 disabled:hover:text-slate-500"
                disabled={firstUnused == null}
                aria-label={editing ? `New ${keyHeader}` : `Add ${keyHeader}`}
                onClick={handleAdd}
              >
                <HiPlus size={18} aria-hidden />
              </button>
            </Tooltip>

            <Tooltip content="Remove selected">
              <button
                type="button"
                className="text-slate-400 hover:text-[var(--bs-danger)] disabled:opacity-30 disabled:hover:text-slate-400"
                disabled={!editing}
                aria-label="Remove selected"
                onClick={handleRemove}
              >
                <HiTrash size={16} aria-hidden />
              </button>
            </Tooltip>
          </div>
        </div>
      </div>

      {/* Pill area: one pill per committed entry; click to load it into the entry row above. */}
      <div className="p-2">
        {rows.length === 0 ? (
          <p className="px-1 text-xs text-slate-400">None yet — pick one above and click +.</p>
        ) : (
          <div className="flex flex-wrap gap-2">
            {rows.map((row, index) => {
              // Cast needed: with a generic TKey, TS can't narrow the indexed-access type for JSX use below.
              const Icon = keyIcons?.[row.key] as IconType | undefined;
              const isSelected = sel === index;
              return (
                <Tooltip key={row.key} content={keyLabels[row.key]}>
                  <button
                    type="button"
                    aria-label={`${keyLabels[row.key]} ${rawToScaled(row.value)}`}
                    onClick={() => togglePill(index)}
                    className={[
                      "inline-flex items-center gap-1 rounded-full border px-2 py-1 text-xs",
                      isSelected
                        ? "border-[var(--bs-primary)] bg-[color-mix(in_srgb,var(--bs-primary)_12%,transparent)] text-[var(--bs-primary)]"
                        : "border-slate-300 bg-white text-slate-700 hover:bg-slate-50",
                    ].join(" ")}
                  >
                    {Icon ? <Icon className="h-3.5 w-3.5" aria-hidden /> : <span>{keyLabels[row.key]}</span>}
                    <span className="tabular-nums">{rawToScaled(row.value)}</span>
                  </button>
                </Tooltip>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}
