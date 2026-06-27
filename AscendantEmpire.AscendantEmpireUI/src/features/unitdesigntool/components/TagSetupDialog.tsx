// TagSetupDialog — a simple single-page Modal for authoring a single global Tag in the dev Unit
// Designer. Tag has only four fields, so (unlike ComponentSetupDialog's PagedDialog) a plain Modal is
// enough. Controlled local state is seeded from `createEmptyTag()` and re-seeded on every open.
//
// The dialog is create-focused for now ("Add Tag"); editing an existing tag (the stubbed "Search Tag"
// flow) can pass `initialTag` later without changing the form.

import { useEffect, useState } from "react";

import { Button, CodeField, Modal } from "@/components/ui";

import { FieldRow, inputClass } from "./componentSetupFields";
import { createEmptyTag, TAG_TYPES, type Tag, type TagType } from "../types/Tag";

interface TagSetupDialogProps {
  open: boolean;
  onCancel: () => void;
  /** Optional starting tag (for editing). When omitted, a blank tag is created. */
  initialTag?: Tag;
  /** Called with the edited tag when the user commits. */
  onCommit: (tag: Tag) => void;
}

export default function TagSetupDialog({ open, onCancel, initialTag, onCommit }: TagSetupDialogProps) {
  // The working copy of the tag being authored.
  const [tag, setTag] = useState<Tag>(() => initialTag ?? createEmptyTag());

  // Re-seed the form each time the dialog opens so a previous edit never leaks into a new "Add".
  useEffect(() => {
    if (open) {
      setTag(initialTag ?? createEmptyTag());
    }
  }, [open, initialTag]);

  // Code and DisplayName are required; the backend is authoritative, this just gates the Save button.
  const canSave = tag.code.trim() !== "" && tag.displayName.trim() !== "";

  const handleCommit = () => {
    if (!canSave) {
      return;
    }

    onCommit(tag);
  };

  return (
    <Modal
      open={open}
      title="Tag Setup"
      onClose={onCancel}
      footer={
        <>
          <Button variant="secondary" onClick={onCancel}>
            Cancel / Close
          </Button>
          <Button variant="success" disabled={!canSave} onClick={handleCommit}>
            Save
          </Button>
        </>
      }>
      {/* Two-column rows (label left, control right) matching the other manager dialogs via FieldRow. */}
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
            // Treat an empty box as "no description" so we don't store blank strings.
            value={tag.description ?? ""}
            placeholder="Optional free-text description"
            onChange={(e) => setTag((prev) => ({ ...prev, description: e.target.value === "" ? null : e.target.value }))}
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
    </Modal>
  );
}
