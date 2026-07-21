// TagTypeSetupDialog — a PagedDialog for editing the fixed set of Tag Types in the dev Entity Setup
// Tool. The set is fixed by the backend `TagType` enum, so (like the Biome/Geography Managers) there is
// no "create new" flow: a selection box at the top picks which existing tag type to edit, and Save
// bulk-upserts the whole edited collection.
//
// Editing is done in place on a local draft array seeded from `initialTagTypes` on every open; the
// parent persists the draft via `onCommit`.

import { useEffect, useMemo, useState } from "react";

import { Alert, Button, PagedDialog } from "@/components/ui";
import { isValidColor, pickTextColorOnBg } from "@/util/color";

import { FieldRow, inputClass } from "./componentSetupFields";
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
  // previous edit session never leaks in.
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

  // Every row must have a non-blank Display Name and Description; the backend enforces the same, but
  // gating here avoids a round-trip for the common empty-field case.
  const canSave = draft.length > 0 && draft.every((t) => t.displayName.trim() !== "" && t.description.trim() !== "");

  const handleCommit = () => {
    if (!canSave) {
      return;
    }

    onCommit(draft);
  };

  const colour = selected?.colour ?? "";

  return (
    <PagedDialog
      open={open}
      title="Tag Type Setup"
      onCancel={onCancel}
      onCommit={handleCommit}
      header={
        <div className="flex flex-col gap-3">
          {/* Selection box: pick which tag type to edit (the set is fixed by the enum). */}
          <div className="flex flex-wrap items-center gap-2">
            <label className="text-sm font-semibold whitespace-nowrap">Select Tag Type</label>
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
          <Button variant="success" disabled={!canSave} onClick={handleCommit}>
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
              <FieldRow label="Display Name" align="top">
                <input
                  className={inputClass}
                  value={selected?.displayName ?? ""}
                  placeholder="e.g. Classification"
                  disabled={!selected}
                  onChange={(e) => updateField("displayName", e.target.value)}
                />
              </FieldRow>

              <FieldRow label="Description" align="top">
                <textarea
                  className={inputClass}
                  rows={3}
                  value={selected?.description ?? ""}
                  placeholder="Description of what this tag type represents"
                  disabled={!selected}
                  onChange={(e) => updateField("description", e.target.value)}
                />
              </FieldRow>

              <FieldRow label="Is Inherited" align="top">
                <label className="flex items-center gap-2 pt-2 text-sm text-slate-700">
                  <input
                    type="checkbox"
                    checked={selected?.isInherited ?? false}
                    disabled={!selected}
                    onChange={(e) => updateField("isInherited", e.target.checked)}
                  />
                  Inherited
                </label>
              </FieldRow>

              <FieldRow label="Colour" align="top">
                {/* Free-text HTML colour; the input previews the colour as its own background. */}
                <input
                  className={inputClass}
                  value={colour}
                  placeholder="#3366cc"
                  disabled={!selected}
                  // Empty string clears the colour back to null rather than storing "".
                  onChange={(e) => updateField("colour", e.target.value === "" ? null : e.target.value)}
                  style={{
                    backgroundColor: isValidColor(colour) ? colour : undefined,
                    color: isValidColor(colour) ? pickTextColorOnBg(colour) : undefined,
                  }}
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
