// BodyProperties — the Properties-tab editor shown when the component is a Body.
//
// Controlled component: it takes the body slice and emits the full updated body via onChange. A body
// has no sparse key/value tables, so there's no working-row state to manage here.

import { DOMAIN_TYPES, type DomainType, type UnitComponentBody } from "../../../types/UnitComponent";
import { FieldRow, NumberField, inputClass } from "../../componentSetupFields";
import { DOMAIN_TYPE_LABELS } from "../../componentSetupLabels";

interface BodyPropertiesProps {
  body: UnitComponentBody;
  onChange: (next: UnitComponentBody) => void;
}

export default function BodyProperties({ body, onChange }: BodyPropertiesProps) {
  // Patch a single body field and emit the updated body up to the parent.
  const patch = <K extends keyof UnitComponentBody>(field: K, value: UnitComponentBody[K]) => {
    onChange({ ...body, [field]: value });
  };

  return (
    <div className="w-full max-w-xl">
      <FieldRow label="Movement Domain">
        <select
          className={inputClass}
          value={body.movementDomain}
          onChange={(e) => patch("movementDomain", e.target.value as DomainType)}>
          {DOMAIN_TYPES.map((domain) => (
            <option key={domain} value={domain}>
              {DOMAIN_TYPE_LABELS[domain]}
            </option>
          ))}
        </select>
      </FieldRow>

      <FieldRow label="Campaign Movement Points">
        <NumberField value={body.campaignMovementPoints} step={0.1} onChange={(next) => patch("campaignMovementPoints", next)} />
      </FieldRow>

      <FieldRow label="Battlefield Movement Points">
        <NumberField value={body.battlefieldMovementPoints} step={0.1} onChange={(next) => patch("battlefieldMovementPoints", next)} />
      </FieldRow>

      <FieldRow label="Min Strength">
        <NumberField value={body.minStrength} onChange={(next) => patch("minStrength", next)} />
      </FieldRow>

      <FieldRow label="Max Strength">
        <NumberField value={body.maxStrength} onChange={(next) => patch("maxStrength", next)} />
      </FieldRow>

      <FieldRow label="Increment Strength">
        <NumberField value={body.incrementStrength} onChange={(next) => patch("incrementStrength", next)} />
      </FieldRow>
    </div>
  );
}
