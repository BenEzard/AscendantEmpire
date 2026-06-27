// GeneralSection — the dialog's "General" page: the fields common to every component (Code,
// DisplayName, Description, ComponentType, Weight, Detection, Visibility).
//
// Controlled component: it edits the passed-in component and emits the whole updated component via
// onChange. Component-type changes are delegated to the orchestrator (onComponentTypeChange), because
// switching type has to swap the type-specific extension, which lives above this section.

import { useState } from "react";

import ToolbarExclusiveButtons, { type ToolbarExclusiveItem } from "@/components/ToolbarExclusiveButtons";

import { COMPONENT_TYPES, type ComponentType, type UnitComponent } from "../../types/UnitComponent";
import ScaledNumberField from "../ScaledNumberField";
import { componentTypeIcons } from "../componentTypeIcons";
import { FieldRow, inputClass } from "../componentSetupFields";

// Derive a component code from the display name: lowercase, with runs of whitespace collapsed to "_".
function toComponentCode(displayName: string): string {
  return displayName.trim().toLowerCase().replace(/\s+/g, "_");
}

interface GeneralSectionProps {
  component: UnitComponent;
  onChange: (next: UnitComponent) => void;
  // Delegated to the orchestrator: changing type swaps the matching extension object in/out.
  onComponentTypeChange: (next: ComponentType) => void;
}

export default function GeneralSection({ component, onChange, onComponentTypeChange }: GeneralSectionProps) {
  // Tracks whether the user has hand-edited Code; once they have, we stop auto-deriving it from the
  // display name. This resets to false whenever the dialog reopens (Modal unmounts this section on close).
  const [isCodeManuallyEdited, setIsCodeManuallyEdited] = useState(false);

  // Patch a single top-level component field and emit the updated component.
  const patch = <K extends keyof UnitComponent>(field: K, value: UnitComponent[K]) => {
    onChange({ ...component, [field]: value });
  };

  // One mutually-exclusive icon button per component type, each showing the type's icon with the type
  // name as its tooltip.
  const componentTypeItems: ToolbarExclusiveItem[] = COMPONENT_TYPES.map((type) => {
    const Icon = componentTypeIcons[type];
    return { type: "button", key: type, icon: <Icon size={18} aria-hidden />, tooltip: type };
  });

  return (
    <div className="w-full max-w-xl">
      <FieldRow label="Display Name">
        <input
          className={inputClass}
          value={component.displayName}
          onChange={(e) => {
            const nextDisplayName = e.target.value;
            // Keep Code in sync with the display name until the user takes manual control of it.
            onChange({
              ...component,
              displayName: nextDisplayName,
              code: isCodeManuallyEdited ? component.code : toComponentCode(nextDisplayName),
            });
          }}
        />
      </FieldRow>

      <FieldRow label="Code">
        <input
          className={inputClass}
          value={component.code}
          placeholder="e.g. rifle_mk1"
          onChange={(e) => {
            setIsCodeManuallyEdited(true);
            patch("code", e.target.value);
          }}
        />
      </FieldRow>

      <FieldRow label="Description">
        <textarea
          className={inputClass}
          rows={3}
          value={component.description ?? ""}
          // Empty box means "no description" (null), matching the nullable backend column.
          onChange={(e) => patch("description", e.target.value === "" ? null : e.target.value)}
        />
      </FieldRow>

      <FieldRow label="Component Type">
        <ToolbarExclusiveButtons
          items={componentTypeItems}
          value={component.componentType}
          onChange={(next) => onComponentTypeChange(next as ComponentType)}
        />
      </FieldRow>

      <FieldRow label="Weight">
        <ScaledNumberField rawValue={component.weight} onChange={(next) => patch("weight", next ?? 0)} />
      </FieldRow>

      <FieldRow label="Detection">
        <ScaledNumberField rawValue={component.detection} nullable onChange={(next) => patch("detection", next)} />
      </FieldRow>

      <FieldRow label="Visibility">
        <ScaledNumberField rawValue={component.visibility} nullable onChange={(next) => patch("visibility", next)} />
      </FieldRow>
    </div>
  );
}
