// GeneralSection — the dialog's "General" page: the fields common to every component (Code,
// DisplayName, Description, ComponentType, Weight, Detection, Visibility).
//
// Controlled component: it edits the passed-in component and emits the whole updated component via
// onChange. Component-type changes are delegated to the orchestrator (onComponentTypeChange), because
// switching type has to swap the type-specific extension, which lives above this section.

import ToolbarExclusiveButtons, { type ToolbarExclusiveItem } from "@/components/toolbar/ToolbarExclusiveButtons";
import { CodeField } from "@/components/ui";

import { COMPONENT_TYPES, type ComponentType, type UnitComponent } from "@/features/unitdesign/types/UnitComponent";
import ScaledNumberField from "../ScaledNumberField";
import { componentTypeIcons } from "../componentTypeIcons";
import { FieldRow, inputClass } from "../componentSetupFields";

interface GeneralSectionProps {
  component: UnitComponent;
  onChange: (next: UnitComponent) => void;
  // Delegated to the orchestrator: changing type swaps the matching extension object in/out.
  onComponentTypeChange: (next: ComponentType) => void;
}

export default function GeneralSection({ component, onChange, onComponentTypeChange }: GeneralSectionProps) {
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
        <input className={inputClass} value={component.displayName} onChange={(e) => patch("displayName", e.target.value)} />
      </FieldRow>

      <FieldRow label="Code">
        {/* Derives from Display Name (lowercase + underscores) until hand-edited; pencil resets it. */}
        <CodeField value={component.code} source={component.displayName} placeholder="e.g. rifle_mk1" onChange={(code) => patch("code", code)} />
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
