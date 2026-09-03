// BodyProperties — the Properties-tab editor shown when the component is a Body.
//
// Controlled component: it takes the body slice and emits the full updated body via onChange. A body
// has no sparse key/value tables, so there's no working-row state to manage here.

import { rawToScaled, type UnitComponentBody } from "@/features/unitdesign/types/UnitComponent";
import DomainSelection from "@/components/game/DomainSelection";
import ScaledNumberField from "../../ScaledNumberField";
import { FieldRow } from "@/components/ui/FieldRow";
import { calculateBattleMovementPoints } from "@/features/unitdesign/model/battleMovement";

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
          <DomainSelection value={body.movementDomain} onChange={(next) => patch("movementDomain", next)} />
        </FieldRow>

        {/* Movement points are stored as raw Scale-1,000 longs; ScaledNumberField shows the decimal. */}
        <FieldRow label="Campaign Points">
          <ScaledNumberField rawValue={body.campaignMovementPoints} onChange={(next) => patch("campaignMovementPoints", next ?? 0)} />
        </FieldRow>

        {/* Battlefield points are no longer authored: template compilation derives them from Power less the
            weight of the components fitted to the body, so anything typed here would be silently discarded.
            The unloaded figure is still shown, because it is the ceiling every design built on this body
            starts from — fitting kit only ever takes movement away from it. */}
        {/* Padded to match the inputs above so the value sits on the same baseline as the fields it replaces. */}
        <FieldRow label="Battlefield Points">
          <div className="px-2 py-2 text-sm text-slate-700">{rawToScaled(calculateBattleMovementPoints(body.power, 0)).toFixed(2)}</div>
        </FieldRow>
      </fieldset>

      {/* Power and Hitpoints are stored as raw Scale-1,000 longs; ScaledNumberField shows the decimal. */}
      <FieldRow label="Power">
        <ScaledNumberField rawValue={body.power} onChange={(next) => patch("power", next ?? 0)} />
      </FieldRow>

      <FieldRow label="Hitpoints">
        <ScaledNumberField rawValue={body.hitpoints} onChange={(next) => patch("hitpoints", next ?? 0)} />
      </FieldRow>
    </div>
  );
}
