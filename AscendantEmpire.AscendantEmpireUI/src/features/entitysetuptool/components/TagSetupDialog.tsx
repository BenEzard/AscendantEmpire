// TagSetupDialog — a PagedDialog for authoring a single global Tag in the dev Unit Designer. Tag has
// only four fields, so unlike ComponentSetupDialog it uses a single "General" section rather than
// splitting fields across pages. Controlled local state is seeded from `createEmptyTag()` and
// re-seeded on every open.
//
// The dialog is create-focused for now ("Add Tag"); editing an existing tag (the stubbed "Search Tag"
// flow) can pass `initialTag` later without changing the form.

import { useEffect, useState } from "react";

import { Alert, Button, CodeField, PagedDialog } from "@/components/ui";

import { FieldRow, inputClass } from "./componentSetupFields";
import { createEmptyTag, TAG_TYPES, type Tag, type TagType } from "@/features/unitdesign/types/Tag";

interface TagSetupDialogProps {
  open: boolean;
  onCancel: () => void;
  /** Optional starting tag (for editing). When omitted, a blank tag is created. */
  initialTag?: Tag;
  /** Called with the edited tag when the user commits. */
  onCommit: (tag: Tag) => void;
  /** API or validation error from the last commit attempt; shown as a danger alert above the form. */
  error?: string | null;
}

export default function TagSetupDialog({ open, onCancel, initialTag, onCommit, error }: TagSetupDialogProps) {
  // The working copy of the tag being authored.
  const [tag, setTag] = useState<Tag>(() => initialTag ?? createEmptyTag());

  // Re-seed the form each time the dialog opens so a previous edit never leaks into a new "Add".
  useEffect(() => {
    if (open) {
      setTag(initialTag ?? createEmptyTag());
    }
  }, [open, initialTag]);

  // Code, DisplayName, and Description are all required; the backend enforces the same rules, but
  // gating the Save button here avoids a round-trip for the most common empty-field cases.
  const canSave = tag.code.trim() !== "" && tag.displayName.trim() !== "" && tag.description.trim() !== "";

  const handleCommit = () => {
    if (!canSave) {
      return;
    }

    onCommit(tag);
  };

  return (
    <PagedDialog
      open={open}
      title="Tag Setup"
      onCancel={onCancel}
      onCommit={handleCommit}
      header={
        // API or validation error from the last commit attempt.
        error && (
          <Alert variant="danger" className="mb-3">
            {error}
          </Alert>
        )
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
            // Two-column rows (label left, control right) matching the other manager dialogs via FieldRow.
            <div className="flex flex-col">
              <FieldRow label="Display Name" align="top">
                <input
                  className={inputClass}
                  value={tag.displayName}
                  placeholder="e.g. Amphibious"
                  onChange={(e) => setTag((prev) => ({ ...prev, displayName: e.target.value }))}
                />
              </FieldRow>

              <FieldRow label="Code" align="top">
                {/* Derives from Display Name (lowercase + underscores) until hand-edited; pencil resets it. */}
                <CodeField
                  value={tag.code}
                  source={tag.displayName}
                  placeholder="tag code, e.g. amphibious"
                  onChange={(code) => setTag((prev) => ({ ...prev, code }))}
                />
              </FieldRow>

              <FieldRow label="Description" align="top">
                <textarea
                  className={inputClass}
                  rows={3}
                  value={tag.description}
                  placeholder="Description of what this tag provides"
                  onChange={(e) => setTag((prev) => ({ ...prev, description: e.target.value }))}
                />
              </FieldRow>

              <FieldRow label="Tag Type" align="top">
                <select
                  className={inputClass}
                  value={tag.tagType}
                  onChange={(e) => setTag((prev) => ({ ...prev, tagType: e.target.value as TagType }))}>
                  {TAG_TYPES.map((tagType) => (
                    <option key={tagType} value={tagType}>
                      {tagType}
                    </option>
                  ))}
                </select>
              </FieldRow>
            </div>
          ),
        },
      ]}
    />
  );
}
