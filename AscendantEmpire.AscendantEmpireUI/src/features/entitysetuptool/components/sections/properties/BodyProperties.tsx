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
      <fieldset className="rounded-md border border-slate-200 px-3 pb-1 pt-1">
        <legend className="px-1 text-xs font-medium text-slate-500">Movement</legend>

        <FieldRow label="Domain">
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

        <FieldRow label="Campaign Points">
          <NumberField value={body.campaignMovementPoints} step={0.1} onChange={(next) => patch("campaignMovementPoints", next)} />
        </FieldRow>

        <FieldRow label="Battlefield Points">
          <NumberField value={body.battlefieldMovementPoints} step={0.1} onChange={(next) => patch("battlefieldMovementPoints", next)} />
        </FieldRow>
      </fieldset>

      <fieldset className="mt-3 rounded-md border border-slate-200 px-3 pb-1 pt-1">
        <legend className="px-1 text-xs font-medium text-slate-500">Unit Size</legend>

        <FieldRow label="Minimum">
          <NumberField value={body.minimumUnitSize} onChange={(next) => patch("minimumUnitSize", next)} />
        </FieldRow>

        <FieldRow label="Maximum">
          <NumberField value={body.maximumUnitSize} onChange={(next) => patch("maximumUnitSize", next)} />
        </FieldRow>

        <FieldRow label="Increment">
          <NumberField value={body.sizeIncrement} onChange={(next) => patch("sizeIncrement", next)} />
        </FieldRow>
      </fieldset>
    </div>
  );
}
