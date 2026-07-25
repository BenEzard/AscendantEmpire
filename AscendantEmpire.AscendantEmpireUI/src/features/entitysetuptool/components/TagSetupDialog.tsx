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
import type { TagTypeInfo } from "@/features/unitdesign/types/TagType";
import { DOMAIN_TYPES, type DomainType } from "@/features/unitdesign/types/UnitComponent";
import { DOMAIN_TYPE_LABELS } from "@/components/game/domainLabels";

interface TagSetupDialogProps {
  open: boolean;
  onCancel: () => void;
  /** Optional starting tag (for editing). When omitted, a blank tag is created. */
  initialTag?: Tag;
  /** The tag type catalog, driving the "Tag Type" dropdown (option label = display name, value = enum). */
  tagTypes: TagTypeInfo[];
  /** Called with the edited tag when the user commits. */
  onCommit: (tag: Tag) => void;
  /** API or validation error from the last commit attempt; shown as a danger alert above the form. */
  error?: string | null;
}

export default function TagSetupDialog({ open, onCancel, initialTag, tagTypes, onCommit, error }: TagSetupDialogProps) {
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

  // The "Tag Type" options come from the tag type catalog (label = display name, value = enum key). Fall
  // back to the raw enum names if the catalog hasn't loaded, and always ensure the tag's current type is
  // present so an edited tag never shows a blank selection.
  const typeOptions: { value: TagType; label: string }[] =
    tagTypes.length > 0
      ? tagTypes.map((t) => ({ value: t.tagTypeId, label: t.displayName.trim() !== "" ? t.displayName : t.tagTypeId }))
      : TAG_TYPES.map((t) => ({ value: t, label: t }));

  if (!typeOptions.some((option) => option.value === tag.tagType)) {
    typeOptions.unshift({ value: tag.tagType, label: tag.tagType });
  }

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
                  onChange={(e) => {
                    const nextType = e.target.value as TagType;
                    // A movement domain is only valid on an organisation-group tag, so clear it when switching
                    // away from that type (the backend would otherwise reject the pairing).
                    setTag((prev) => ({
                      ...prev,
                      tagType: nextType,
                      movementDomain: nextType === "MilitaryOrganisationalGroup" ? prev.movementDomain : null,
                    }));
                  }}>
                  {typeOptions.map((option) => (
                    <option key={option.value} value={option.value}>
                      {option.label}
                    </option>
                  ))}
                </select>
              </FieldRow>

              {/* Movement Domain — only shown for organisation-group tags. It ties the organisation hierarchy
                  built on this tag to a domain, so its tier depth is capped by that domain's rank ladder. */}
              {tag.tagType === "MilitaryOrganisationalGroup" && (
                <FieldRow label="Movement Domain" align="top">
                  <select
                    className={inputClass}
                    value={tag.movementDomain ?? ""}
                    onChange={(e) => {
                      const value = e.target.value;
                      setTag((prev) => ({ ...prev, movementDomain: value === "" ? null : (value as DomainType) }));
                    }}>
                    <option value="">— none —</option>
                    {DOMAIN_TYPES.map((domain) => (
                      <option key={domain} value={domain}>
                        {DOMAIN_TYPE_LABELS[domain]}
                      </option>
                    ))}
                  </select>
                </FieldRow>
              )}
            </div>
          ),
        },
      ]}
    />
  );
}
