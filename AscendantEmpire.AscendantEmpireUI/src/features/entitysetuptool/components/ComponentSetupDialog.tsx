// ComponentSetupDialog — a PagedDialog for authoring a single UnitComponent in the dev Entity Setup Tool.
//
// This is a thin orchestrator. It owns the working UnitComponent and wires up two pages:
//   • "General"    — GeneralSection: the fields common to every component.
//   • "Properties" — PropertiesSection: the editor for the component's current type (Body/Weapon/Armour;
//                    other types have no type-specific fields yet).
// Each section is a controlled component that takes its slice of the model and emits the updated model
// back; the orchestrator just holds the state, swaps the type-specific extension, and commits.
//
// This is UI-only for now: state is held locally and `onCommit` is a placeholder. Persisting to the
// BulkUpsertUnitComponents endpoint can be wired up later (see types/UnitComponent.ts for the model).

import { useEffect, useState } from "react";

import { Alert, Button, PagedDialog } from "@/components/ui";

import { getTags } from "@/features/unitdesign/api/tagsApi";
import type { Tag } from "@/features/unitdesign/types/Tag";
import type { TagTypeInfo } from "@/features/unitdesign/types/TagType";

import {
  createEmptyArmour,
  createEmptyBody,
  createEmptyComponent,
  createEmptyWeapon,
  damageProfileHasPositiveValue,
  type ComponentType,
  type UnitComponent,
} from "@/features/unitdesign/types/UnitComponent";

import AttackSection from "./sections/AttackSection";
import DefenceSection from "./sections/DefenceSection";
import GeneralSection from "./sections/GeneralSection";
import ImageSection from "./sections/ImageSection";
import PropertiesSection from "./sections/PropertiesSection";
import SlotsSection from "./sections/SlotsSection";
import SupersessionSection from "./sections/SupersessionSection";
import TagsSection from "./sections/TagsSection";

interface ComponentSetupDialogProps {
  open: boolean;
  onCancel: () => void;
  /** Optional starting component (for editing). When omitted, a blank component is created. */
  initialComponent?: UnitComponent;
  /** Called with the edited component when the user commits. */
  onCommit?: (component: UnitComponent) => void;
  /** API or validation error from the last commit attempt; shown as a danger alert above the sections. */
  error?: string | null;
  /** Tag type metadata (colour/icon per type); feeds the Tags page's chip colouring. */
  tagTypes: TagTypeInfo[];
}

export default function ComponentSetupDialog({ open, onCancel, initialComponent, onCommit, error, tagTypes }: ComponentSetupDialogProps) {
  // The working copy of the component being authored.
  const [component, setComponent] = useState<UnitComponent>(() => initialComponent ?? createEmptyComponent());

  // Bumped on every open. Used as the section `key` so the per-type editors remount and re-seed their
  // working-row state *after* the model reset below has landed (this orchestrator itself stays mounted
  // while closed, so the reset arrives via effect — the key guarantees children mount against it).
  const [openInstance, setOpenInstance] = useState(0);

  // PagedDialog itself never unmounts (Modal just renders null while closed), so its own active-section
  // state would otherwise persist across closes — reopening the dialog would land back on whatever page
  // was last viewed. Controlling it here and resetting alongside the component lets us force it back to
  // the first page ("general") on every open.
  const [activeSection, setActiveSection] = useState("general");

  // The global tag catalogue the Tags page's combo box picks from. Fetched fresh on each open so a tag
  // created earlier in the same tool session is immediately selectable. Loading/error feed the combo box.
  const [availableTags, setAvailableTags] = useState<Tag[]>([]);
  const [tagsLoading, setTagsLoading] = useState(false);
  const [tagsError, setTagsError] = useState<string | null>(null);

  useEffect(() => {
    if (open) {
      setComponent(initialComponent ?? createEmptyComponent());
      setOpenInstance((instance) => instance + 1);
      setActiveSection("general");
    }
  }, [open, initialComponent]);

  // Load the tag catalogue whenever the dialog opens. Kept separate from the reset effect above so the
  // async result never races the model reset. A failure surfaces in the combo box (non-fatal).
  useEffect(() => {
    if (!open) {
      return;
    }

    let cancelled = false;
    setTagsLoading(true);
    setTagsError(null);
    getTags()
      .then((tags) => {
        if (!cancelled) {
          setAvailableTags(tags);
        }
      })
      .catch((err) => {
        if (!cancelled) {
          setTagsError("Failed to load tags.");
          console.error("Failed to load tags.", err);
        }
      })
      .finally(() => {
        if (!cancelled) {
          setTagsLoading(false);
        }
      });

    // Ignore an in-flight load if the dialog closes (or reopens) before it resolves.
    return () => {
      cancelled = true;
    };
  }, [open]);

  // Changing the component type makes sure the newly-active type has an extension object to edit, but
  // deliberately keeps the other extensions populated so toggling type never loses in-progress edits.
  // The inactive extensions are collapsed away at Save time (see handleCommit).
  const handleComponentTypeChange = (nextType: ComponentType) => {
    setComponent((prev) => ({
      ...prev,
      componentType: nextType,
      body: nextType === "Body" ? (prev.body ?? createEmptyBody()) : prev.body,
      weapon: nextType === "Weapon" ? (prev.weapon ?? createEmptyWeapon()) : prev.weapon,
      armour: nextType === "Armour" ? (prev.armour ?? createEmptyArmour()) : prev.armour,
    }));
  };

  const handleCommit = () => {
    // Collapse to a valid component before committing. A Weapon keeps only its weapon; Armour only its
    // armour. A Body keeps its body plus any optional inherent weapon/armour — but only when that
    // weapon/armour actually carries a positive value; an empty (all-zero) attack/defence is dropped so
    // a body without inherent combat stats stays lean. Extensions belonging to a type the component no
    // longer is (e.g. a stale body left over from toggling type), and in-progress edits for inactive
    // types, are dropped.
    const { componentType } = component;
    const isBody = componentType === "Body";
    const keepBodyWeapon = isBody && component.weapon != null && damageProfileHasPositiveValue(component.weapon.attack);
    const keepBodyArmour = isBody && component.armour != null && damageProfileHasPositiveValue(component.armour.resistance);
    onCommit?.({
      ...component,
      body: isBody ? component.body : null,
      weapon: componentType === "Weapon" ? component.weapon : keepBodyWeapon ? component.weapon : null,
      armour: componentType === "Armour" ? component.armour : keepBodyArmour ? component.armour : null,
    });
  };

  return (
    <PagedDialog
      open={open}
      title="Component Setup"
      // Static panel size so the dialog never resizes between pages (or as content is edited). 896px is
      // the existing "lg" width; 500px gives the tallest page ("Attack (Body)", ~424px empty) some
      // headroom so a few added rows fit before the content scrolls.
      width={896}
      height={500}
      panelClassName="thick-border-modal"
      onCancel={onCancel}
      onCommit={handleCommit}
      activeSection={activeSection}
      onSectionChange={setActiveSection}
      header={
        error != null && (
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
            <GeneralSection
              key={openInstance}
              component={component}
              onChange={setComponent}
              onComponentTypeChange={handleComponentTypeChange}
            />
          ),
        },
        {
          key: "properties",
          label: "Properties",
          content: <PropertiesSection key={openInstance} component={component} onChange={setComponent} />,
        },
        {
          // Only bodies expose slots, so this page is disabled (greyed) for every other component type.
          key: "slots",
          label: "Slots",
          disabled: component.componentType !== "Body",
          // Live slot count, shown as "Slots (2)" in the nav. Read off the same array that feeds the page
          // below, so adding or removing a slot moves the number with it.
          count: component.body?.slots.length ?? 0,
          content: (
            <SlotsSection
              key={openInstance}
              slots={component.body?.slots ?? []}
              onChange={(slots) =>
                setComponent((prev) => (prev.body != null ? { ...prev, body: { ...prev.body, slots } } : prev))
              }
            />
          ),
        },
        {
          key: "image",
          label: "Image",
          content: <ImageSection key={openInstance} component={component} onChange={setComponent} />,
        },
        {
          key: "tags",
          label: "Tags",
          // Live count of attached tags, shown as "Tags (3)" in the nav.
          count: component.tagCodes.length,
          content: (
            <TagsSection
              key={openInstance}
              tagCodes={component.tagCodes}
              onChange={(tagCodes) => setComponent((prev) => ({ ...prev, tagCodes }))}
              availableTags={availableTags}
              tagTypes={tagTypes}
              loading={tagsLoading}
              error={tagsError}
            />
          ),
        },
        {
          key: "supersession",
          label: "Supersession",
          content: (
            <SupersessionSection
              key={openInstance}
              supersededBy={component.supersededBy}
              onChange={(supersededBy) => setComponent((prev) => ({ ...prev, supersededBy }))}
            />
          ),
        },
        {
          // A body's optional inherent attack; disabled (greyed) for every other component type.
          key: "attack",
          label: "Attack (Body)",
          disabled: component.componentType !== "Body",
          content: <AttackSection key={openInstance} component={component} onChange={setComponent} />,
        },
        {
          // A body's optional inherent defence; disabled (greyed) for every other component type.
          key: "defence",
          label: "Defence (Body)",
          disabled: component.componentType !== "Body",
          content: <DefenceSection key={openInstance} component={component} onChange={setComponent} />,
        },
      ]}
    />
  );
}
