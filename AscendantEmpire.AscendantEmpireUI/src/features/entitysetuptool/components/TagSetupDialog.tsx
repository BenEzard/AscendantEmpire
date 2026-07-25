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

  // Whether the user has pressed Save at least once. Validation messages stay hidden until then (so a
  // freshly opened form isn't pre-littered with "required" errors), and from that point on they update
  // live as the user fills each field in.
  const [submitted, setSubmitted] = useState(false);

  // Re-seed the form each time the dialog opens so a previous edit never leaks into a new "Add", and
  // reset the submitted flag so validation starts quiet again.
  useEffect(() => {
    if (open) {
      setTag(initialTag ?? createEmptyTag());
      setSubmitted(false);
    }
  }, [open, initialTag]);

  // Required-field validation. Rather than disabling Save (which leaves the user guessing why it's
  // greyed out), Save is always clickable; pressing it runs these checks and surfaces any problems.
  // The backend enforces the same rules, so this is a fast local pre-check, not the source of truth.
  // Description is intentionally omitted: it's optional (nullable column, no server-side requirement).
  const fieldErrors: Partial<Record<"displayName" | "code", string>> = {};
  if (tag.displayName.trim() === "") {
    fieldErrors.displayName = "Display Name is required.";
  }
  if (tag.code.trim() === "") {
    fieldErrors.code = "Code is required.";
  }

  // Only reveal errors once the user has attempted a save; they then drive the inline per-field messages.
  const shownErrors: typeof fieldErrors = submitted ? fieldErrors : {};

  // Appended to a control's class when its field is in error to give it a red border. The `!` forces
  // the colour to win over inputClass's default slate border regardless of Tailwind's rule ordering.
  const errorClass = (hasError: boolean) => (hasError ? `${inputClass} !border-red-500` : inputClass);

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
    // Reveal validation now that a save was attempted, then bail if anything is invalid.
    setSubmitted(true);
    if (Object.keys(fieldErrors).length > 0) {
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
        // API or validation error from the last commit attempt. Required-field problems are shown
        // inline beneath each field rather than summarised here.
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
          <Button variant="success" onClick={handleCommit}>
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

              <FieldRow label="Display Name" align="top" error={shownErrors.displayName}>
                <input
                  className={errorClass(!!shownErrors.displayName)}
                  value={tag.displayName}
                  placeholder="e.g. Amphibious"
                  onChange={(e) => setTag((prev) => ({ ...prev, displayName: e.target.value }))}
                />
              </FieldRow>

              <FieldRow label="Code" align="top" error={shownErrors.code}>
                {/* Derives from Display Name (lowercase + underscores) until hand-edited; pencil resets it. */}
                <CodeField
                  value={tag.code}
                  source={tag.displayName}
                  placeholder="tag code, e.g. amphibious"
                  className={errorClass(!!shownErrors.code)}
                  onChange={(code) => setTag((prev) => ({ ...prev, code }))}
                />
              </FieldRow>

              {/* Description is optional, so no validation/error styling here. */}
              <FieldRow label="Description" align="top">
                <textarea
                  className={inputClass}
                  rows={3}
                  value={tag.description}
                  placeholder="Description of what this tag provides"
                  onChange={(e) => setTag((prev) => ({ ...prev, description: e.target.value }))}
                />
              </FieldRow>

              {/* Movement Domain — only meaningful for organisation-group tags. It ties the organisation
                  hierarchy built on this tag to a domain, so its tier depth is capped by that domain's rank
                  ladder. The row is always shown for layout stability, but stays disabled (read-only) until
                  the tag type is Military Organisation Group, at which point it becomes editable. */}
              {(() => {
                // The control is only accessible when the selected tag type is an organisation group.
                const movementDomainEnabled = tag.tagType === "MilitaryOrganisationalGroup";
                return (
                  // `-mt-1` trims half of the Description row's `mb-2` so Movement Domain sits closer to it.
                  <FieldRow label="Movement Domain" align="top" className="-mt-1">
                    <select
                      // Dim the control and show a not-allowed cursor while it's disabled so the read-only
                      // state reads clearly rather than looking like a normal, empty dropdown.
                      className={
                        movementDomainEnabled
                          ? inputClass
                          : `${inputClass} cursor-not-allowed bg-slate-100 text-slate-400`
                      }
                      disabled={!movementDomainEnabled}
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
                );
              })()}

              {/* System Created — flags a tag as seeded/built-in rather than user-authored. Defaults to
                  false; editable here so dev tooling can set it explicitly. */}
              <FieldRow label="System Created">
                <label className="flex items-center gap-2 text-sm text-slate-700">
                  <input
                    type="checkbox"
                    checked={tag.systemCreated}
                    onChange={(e) => setTag((prev) => ({ ...prev, systemCreated: e.target.checked }))}
                  />
                </label>
              </FieldRow>
            </div>
          ),
        },
      ]}
    />
  );
}
