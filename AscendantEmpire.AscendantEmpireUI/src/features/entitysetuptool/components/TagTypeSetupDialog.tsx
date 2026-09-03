// TagTypeSetupDialog — a PagedDialog for editing the fixed set of Tag Types in the dev Entity Setup
// Tool. The set is fixed by the backend `TagType` enum, so (like the Biome/Geography Managers) there is
// no "create new" flow: a selection box at the top picks which existing tag type to edit, and Save
// bulk-upserts the whole edited collection.
//
// Editing is done in place on a local draft array seeded from `initialTagTypes` on every open; the
// parent persists the draft via `onCommit`.

import { useEffect, useMemo, useState } from "react";

import { Alert, Button, ColorPicker, PagedDialog, dialogTitle, type UsedColor } from "@/components/ui";
import { useValidatedCommit } from "@/hooks/useValidatedCommit";

import { FieldRow, inputClass } from "@/components/ui/FieldRow";
import { errorClass } from "@/components/ui/fieldStyles";
import type { TagType } from "@/features/unitdesign/types/Tag";
import type { TagTypeInfo } from "@/features/unitdesign/types/TagType";

interface TagTypeSetupDialogProps {
  open: boolean;
  onCancel: () => void;
  /** The current tag type set to edit. The dialog seeds its draft from this each time it opens. */
  initialTagTypes: TagTypeInfo[];
  /** Called with the whole edited set when the user commits (the backend upserts every row). */
  onCommit: (tagTypes: TagTypeInfo[]) => void;
  /** API or validation error from the last commit attempt; shown as a danger alert above the form. */
  error?: string | null;
}

export default function TagTypeSetupDialog({ open, onCancel, initialTagTypes, onCommit, error }: TagTypeSetupDialogProps) {
  // The working copy of the tag type set, edited in place.
  const [draft, setDraft] = useState<TagTypeInfo[]>(initialTagTypes);

  // Which tag type is currently selected for editing (its enum key), or null when the set is empty.
  const [selectedId, setSelectedId] = useState<TagType | null>(null);

  // Re-seed the draft and default the selection to the first row each time the dialog opens, so a
  // previous edit session never leaks in. (useValidatedCommit resets the revealed-errors flag on open.)
  useEffect(() => {
    if (open) {
      setDraft(initialTagTypes);
      setSelectedId(initialTagTypes[0]?.tagTypeId ?? null);
    }
  }, [open, initialTagTypes]);

  const selected = useMemo(() => draft.find((t) => t.tagTypeId === selectedId) ?? null, [draft, selectedId]);

  // Update one field on the selected row, mapping it in place inside the draft array.
  const updateField = <K extends keyof TagTypeInfo>(field: K, value: TagTypeInfo[K]) => {
    if (!selected) {
      return;
    }

    setDraft((prev) => prev.map((t) => (t.tagTypeId === selected.tagTypeId ? { ...t, [field]: value } : t)));
  };

  // Required-field validation for a single row: Display Name and Description must both be non-blank.
  // The backend enforces the same rules, so this is a fast local pre-check, not the source of truth.
  const rowErrors = (tagType: TagTypeInfo): Partial<Record<"displayName" | "description", string>> => {
    const errors: Partial<Record<"displayName" | "description", string>> = {};
    if (tagType.displayName.trim() === "") {
      errors.displayName = "Display Name is required.";
    }
    if (tagType.description.trim() === "") {
      errors.description = "Description is required.";
    }
    return errors;
  };

  // Only one row is on screen at a time, so if some *other* row is invalid the user would never see why
  // Save "did nothing" — the commit therefore jumps the selection to the first offending row.
  const firstInvalid = draft.find((tagType) => Object.keys(rowErrors(tagType)).length > 0) ?? null;

  const { submitted, attemptCommit } = useValidatedCommit({
    open,
    // An empty draft has nothing to upsert, so it never commits (and there is no row to jump to).
    isValid: draft.length > 0 && firstInvalid === null,
    locate: () => {
      if (firstInvalid) {
        setSelectedId(firstInvalid.tagTypeId);
      }
    },
    onValid: () => onCommit(draft),
  });

  // Errors for the currently selected row, revealed inline only once a save has been attempted.
  const shownErrors: Partial<Record<"displayName" | "description", string>> =
    submitted && selected ? rowErrors(selected) : {};

  const colour = selected?.colour ?? null;

  // Colours already taken by the *other* tag types, so the picker can strike them out and name their owner.
  // The selected row is left out deliberately: the picker exempts its own value anyway, which is what lets
  // pre-existing data that already shares a colour keep showing its selection rather than looking blocked.
  const usedColours = useMemo<UsedColor[]>(() => {
    return draft
      .filter((tagType) => tagType.tagTypeId !== selectedId && tagType.colour !== null && tagType.colour.trim() !== "")
      .map((tagType) => ({
        value: tagType.colour!,
        usedBy: tagType.displayName.trim() !== "" ? tagType.displayName : tagType.tagTypeId,
      }));
  }, [draft, selectedId]);

  return (
    <PagedDialog
      open={open}
      // The tag type picked in the selection box names the dialog; an empty set (or a blank display name)
      // falls back to the bare "Tag Type Setup".
      title={dialogTitle("Tag Type Setup", selected?.displayName)}
      onCancel={onCancel}
      onCommit={attemptCommit}
      header={
        <div className="flex flex-col gap-3 pb-2">
          {/* Selection box: pick which tag type to edit (the set is fixed by the enum). */}
          <div className="flex flex-wrap items-center gap-2">
            <label className="text-sm text-slate-700 whitespace-nowrap">Tag Type:</label>
            <select
              className="w-64 max-w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
              value={selectedId ?? ""}
              onChange={(e) => setSelectedId((e.target.value || null) as TagType | null)}
              disabled={draft.length === 0}>
              {draft.length === 0 ? (
                <option value="">(no tag types)</option>
              ) : (
                draft.map((tagType) => (
                  <option key={tagType.tagTypeId} value={tagType.tagTypeId}>
                    {tagType.displayName.trim() !== "" ? tagType.displayName : tagType.tagTypeId}
                  </option>
                ))
              )}
            </select>
          </div>

          {/* API or validation error from the last commit attempt. */}
          {error && <Alert variant="danger">{error}</Alert>}
        </div>
      }
      footer={
        <>
          <Button variant="secondary" onClick={onCancel}>
            Cancel / Close
          </Button>
          <Button variant="success" onClick={attemptCommit}>
            Save
          </Button>
        </>
      }
      sections={[
        {
          key: "general",
          label: "General",
          content: (
            <div className="flex flex-col">
              <FieldRow label="Display Name" align="top" error={shownErrors.displayName}>
                <input
                  className={errorClass(!!shownErrors.displayName)}
                  value={selected?.displayName ?? ""}
                  placeholder="e.g. Classification"
                  disabled={!selected}
                  onChange={(e) => updateField("displayName", e.target.value)}
                />
              </FieldRow>

              <FieldRow label="Description" align="top" error={shownErrors.description}>
                <textarea
                  className={errorClass(!!shownErrors.description)}
                  rows={3}
                  value={selected?.description ?? ""}
                  placeholder="Description of what this tag type represents"
                  disabled={!selected}
                  onChange={(e) => updateField("description", e.target.value)}
                />
              </FieldRow>

              <FieldRow label="Is Inherited To Base">
                <label className="flex items-center gap-2 text-sm text-slate-700">
                  <input
                    type="checkbox"
                    checked={selected?.isInheritedToBase ?? false}
                    disabled={!selected}
                    onChange={(e) => updateField("isInheritedToBase", e.target.checked)}
                  />
                </label>
              </FieldRow>

              <FieldRow label="Colour">
                {/* Swatch popup over the Tailwind palette. Custom entry is kept on so any HTML colour is still
                    reachable (as the free-text field it replaced allowed), and "No colour" clears back to null. */}
                <ColorPicker
                  value={colour}
                  onChange={(next) => updateField("colour", next)}
                  usedColors={usedColours}
                  allowCustomColor
                  allowClear
                  disabled={!selected}
                  title="Tag type colour"
                />
              </FieldRow>

              <FieldRow label="Icon" align="top">
                <input
                  className={inputClass}
                  value={selected?.icon ?? ""}
                  placeholder="icon identifier"
                  disabled={!selected}
                  onChange={(e) => updateField("icon", e.target.value === "" ? null : e.target.value)}
                />
              </FieldRow>
            </div>
          ),
        },
      ]}
    />
  );
}
