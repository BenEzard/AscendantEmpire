// TagSetupDialog — a PagedDialog for authoring a single global Tag in the dev Unit Designer. The form
// is split across two pages: "General" carries the fields every tag has (type, name, code, description,
// system-created), and "Tag Specifics" carries the organisation-group-only fields (movement domain,
// leadership model, unled sizing). Controlled local state is seeded from `createEmptyTag()` and
// re-seeded on every open.
//
// The dialog is create-focused for now ("Add Tag"); editing an existing tag (the stubbed "Search Tag"
// flow) can pass `initialTag` later without changing the form.

import { useEffect, useState } from "react";

import { Alert, Button, CodeField, PagedDialog, dialogTitle } from "@/components/ui";
import { useValidatedCommit } from "@/hooks/useValidatedCommit";

import { FieldRow, inputClass } from "@/components/ui/FieldRow";
import { errorClass } from "@/components/ui/fieldStyles";
import {
  createEmptyTag,
  LEADERSHIP_MODELS,
  normaliseTag,
  TAG_TYPES,
  validateTag,
  type LeadershipModel,
  type Tag,
  type TagFieldErrors,
  type TagType,
} from "@/features/unitdesign/types/Tag";
import type { TagTypeInfo } from "@/features/unitdesign/types/TagType";
import { DOMAIN_TYPES, type DomainType } from "@/features/unitdesign/types/UnitComponent";
import { DOMAIN_TYPE_LABELS } from "@/components/game/domainLabels";

/** The dialog's pages, in nav order. */
type TagSection = "general" | "specifics";

interface TagSetupDialogProps {
  open: boolean;
  onCancel: () => void;
  /** Optional starting tag (for editing). When omitted, a blank tag is created. */
  initialTag?: Tag;
  /** The tag type catalogue, driving the "Tag Type" dropdown (option label = display name, value = enum). */
  tagTypes: TagTypeInfo[];
  /** Called with the edited tag when the user commits. */
  onCommit: (tag: Tag) => void;
  /** API or validation error from the last commit attempt; shown as a danger alert above the form. */
  error?: string | null;
}

export default function TagSetupDialog({ open, onCancel, initialTag, tagTypes, onCommit, error }: TagSetupDialogProps) {
  // The working copy of the tag being authored.
  const [tag, setTag] = useState<Tag>(() => initialTag ?? createEmptyTag());

  // The visible page. Held here (rather than left to PagedDialog's own internal state) so that a failed
  // save can jump the user to whichever page is carrying the problem.
  const [activeSection, setActiveSection] = useState<TagSection>("general");

  // Re-seed the form each time the dialog opens so a previous edit never leaks into a new "Add", and
  // return to the first page. (useValidatedCommit resets the revealed-errors flag on open itself.)
  useEffect(() => {
    if (open) {
      setTag(initialTag ?? createEmptyTag());
      setActiveSection("general");
    }
  }, [open, initialTag]);

  // Required-field validation; the rules themselves live on the model, in Tag.ts.
  const fieldErrors = validateTag(tag);

  const { submitted, attemptCommit } = useValidatedCommit({
    open,
    isValid: Object.keys(fieldErrors).length === 0,
    // The unled sizing is the only validation living on the "Tag Specifics" page, so when that's the sole
    // problem the user would otherwise press Save on the General page and see nothing happen. Land on
    // whichever page carries the error; General wins when both do, since it comes first.
    locate: () => setActiveSection(!fieldErrors.displayName && !fieldErrors.code ? "specifics" : "general"),
    onValid: () => onCommit(tag),
  });

  // Only reveal errors once the user has attempted a save; they then drive the inline per-field messages.
  const shownErrors: TagFieldErrors = submitted ? fieldErrors : {};

  // The "Tag Type" options come from the tag type catalogue (label = display name, value = enum key). Fall
  // back to the raw enum names if the catalogue hasn't loaded, and always ensure the tag's current type is
  // present so an edited tag never shows a blank selection.
  const typeOptions: { value: TagType; label: string }[] =
    tagTypes.length > 0
      ? tagTypes.map((t) => ({ value: t.tagTypeId, label: t.displayName.trim() !== "" ? t.displayName : t.tagTypeId }))
      : TAG_TYPES.map((t) => ({ value: t, label: t }));

  if (!typeOptions.some((option) => option.value === tag.tagType)) {
    typeOptions.unshift({ value: tag.tagType, label: tag.tagType });
  }

  return (
    <PagedDialog<TagSection>
      open={open}
      // The tag being authored names the dialog once it has a display name; until then the bare
      // "Tag Setup" is kept rather than showing a dangling colon on a blank name.
      title={dialogTitle("Tag Setup", tag.displayName)}
      onCancel={onCancel}
      onCommit={attemptCommit}
      activeSection={activeSection}
      onSectionChange={setActiveSection}
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
          <Button variant="success" onClick={attemptCommit}>
            Save
          </Button>
        </>
      }
      sections={[
        {
          key: "general",
          label: "General",
          // Marks the nav button when this page is holding a validation error. Driven off `shownErrors`
          // (not `fieldErrors`) so the marker appears only once a save has been attempted, in step with
          // the inline per-field messages.
          invalid: !!(shownErrors.displayName || shownErrors.code),
          content: (
            // Two-column rows (label left, control right) matching the other manager dialogs via FieldRow.
            <div className="flex flex-col">
              <FieldRow label="Tag Type" align="top">
                <select
                  className={inputClass}
                  value={tag.tagType}
                  onChange={(e) => {
                    const nextType = e.target.value as TagType;
                    // normaliseTag drops the organisation-group-only fields when switching away from that
                    // type (the backend would otherwise reject the pairing).
                    setTag((prev) => normaliseTag({ ...prev, tagType: nextType }));
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
        {
          key: "specifics",
          label: "Tag Specifics",
          // The unled sizing is the only validation on this page — see the note on "general" above.
          invalid: !!shownErrors.unledSizes,
          content: (
            // Every field on this page belongs to an organisation-group tag. The rows are always shown (for
            // layout stability) but stay disabled — read-only — until the tag type is Military Organisation
            // Group, at which point they become editable.
            <div className="flex flex-col">
              {(() => {
                // The shared gate for the whole page: only an organisation-group tag can author these.
                const organisationGroup = tag.tagType === "MilitaryOrganisationalGroup";
                // The unled sizes carry the extra condition that the leadership model is actually Unled.
                const unledEnabled = organisationGroup && tag.leadershipModel === "Unled";
                // Dims the control and shows a not-allowed cursor while it's disabled, so the read-only
                // state reads clearly rather than looking like a normal, empty dropdown.
                const disabledClass = `${inputClass} cursor-not-allowed bg-slate-100 text-slate-400`;

                // Maps a number input's raw string onto the nullable model field (blank -> null).
                const parseSize = (raw: string): number | null => (raw === "" ? null : Number(raw));

                return (
                  <>
                    {/* Movement Domain — ties the organisation hierarchy built on this tag to a domain, so
                        its tier depth is capped by that domain's rank ladder. */}
                    <FieldRow label="Movement Domain" align="top">
                      <select
                        className={organisationGroup ? inputClass : disabledClass}
                        disabled={!organisationGroup}
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

                    {/* Leadership Model + unled sizing. Commissioned formations are sized by their
                        player-authored structure tiers under the officer rank ceilings; Unled formations
                        (settlers, scouts, drones) have no officers and are sized here on the tag: any whole
                        multiple of Base up to Max, with Default used when no size is chosen (e.g. game-start
                        spawns). See docs/design/units/military-organisation.md, "Leadership model". */}
                    <FieldRow label="Leadership Model" align="top" className="-mt-1">
                      <select
                        className={organisationGroup ? inputClass : disabledClass}
                        disabled={!organisationGroup}
                        value={tag.leadershipModel ?? ""}
                        onChange={(e) => {
                          const value = e.target.value;
                          const nextModel = value === "" ? null : (value as LeadershipModel);
                          // The unled sizes are only valid while the model is Unled; normaliseTag drops them.
                          setTag((prev) => normaliseTag({ ...prev, leadershipModel: nextModel }));
                        }}>
                        <option value="">— none —</option>
                        {LEADERSHIP_MODELS.map((model) => (
                          <option key={model} value={model}>
                            {model}
                          </option>
                        ))}
                      </select>
                    </FieldRow>

                    <FieldRow label="Unled Sizes" align="top" error={shownErrors.unledSizes}>
                      <div className="flex gap-2">
                        <input
                          type="number"
                          min={1}
                          className={unledEnabled ? errorClass(!!shownErrors.unledSizes) : disabledClass}
                          disabled={!unledEnabled}
                          value={tag.unledBaseSize ?? ""}
                          placeholder="base"
                          title="Base (quantum) — every legal size is a whole multiple of this"
                          onChange={(e) => setTag((prev) => ({ ...prev, unledBaseSize: parseSize(e.target.value) }))}
                        />
                        <input
                          type="number"
                          min={1}
                          className={unledEnabled ? errorClass(!!shownErrors.unledSizes) : disabledClass}
                          disabled={!unledEnabled}
                          value={tag.unledDefaultSize ?? ""}
                          placeholder="default"
                          title="Default — the size used when a unit is created without an explicit choice"
                          onChange={(e) => setTag((prev) => ({ ...prev, unledDefaultSize: parseSize(e.target.value) }))}
                        />
                        <input
                          type="number"
                          min={1}
                          className={unledEnabled ? errorClass(!!shownErrors.unledSizes) : disabledClass}
                          disabled={!unledEnabled}
                          value={tag.unledMaxSize ?? ""}
                          placeholder="max"
                          title="Max — the largest a single unled unit may be"
                          onChange={(e) => setTag((prev) => ({ ...prev, unledMaxSize: parseSize(e.target.value) }))}
                        />
                      </div>
                    </FieldRow>
                  </>
                );
              })()}
            </div>
          ),
        },
      ]}
    />
  );
}
