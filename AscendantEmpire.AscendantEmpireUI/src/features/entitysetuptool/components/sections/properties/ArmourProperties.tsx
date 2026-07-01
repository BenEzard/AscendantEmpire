// ArmourProperties — the Properties-tab editor shown when the component is Armour.
//
// Controlled component owning its own working-row state for the single sparse editor (Resistance).
// Same pattern as WeaponProperties: rows are seeded from the incoming armour on mount, and every
// change rebuilds a fully-hydrated resistance profile and emits the updated armour via onChange.

import { useState } from "react";

import type { DamageType } from "@/features/unitdesign/types/DamageType";
import { ALL_DAMAGE_TYPES } from "@/features/unitdesign/types/UnitComponent";
import { DAMAGE_TYPE_ICONS } from "@/features/unitdesign/types/damageTypeBadges";

import {
  damageProfileToRows,
  rowsToDamageProfile,
  type KeyValueRow,
  type UnitComponentArmour,
} from "../../../types/UnitComponent";
import PillKeyValueEditor from "../../PillKeyValueEditor";
import { DAMAGE_TYPE_LABELS } from "../../componentSetupLabels";

interface ArmourPropertiesProps {
  armour: UnitComponentArmour;
  onChange: (next: UnitComponentArmour) => void;
}

export default function ArmourProperties({ armour, onChange }: ArmourPropertiesProps) {
  // Working rows seeded once from the incoming armour's resistance profile.
  const [resistanceRows, setResistanceRows] = useState<KeyValueRow<DamageType>[]>(() => damageProfileToRows(armour.resistance));

  const handleResistanceChange = (next: KeyValueRow<DamageType>[]) => {
    setResistanceRows(next);
    // Rebuild the hydrated (zero-filled) resistance profile and emit the updated armour.
    onChange({ ...armour, resistance: rowsToDamageProfile(next) });
  };

  return (
    <div className="w-full max-w-xl">
      <div className="mb-1 text-sm font-semibold text-slate-700">Resistance</div>
      <p className="mb-2 text-xs text-slate-400">Flat damage reduction subtracted from incoming damage of each type.</p>
      <PillKeyValueEditor
        rows={resistanceRows}
        onChange={handleResistanceChange}
        allKeys={ALL_DAMAGE_TYPES}
        keyLabels={DAMAGE_TYPE_LABELS}
        keyIcons={DAMAGE_TYPE_ICONS}
        keyHeader="Damage Type"
        valueHeader="Resistance"
        defaultValueRaw={0}
      />
    </div>
  );
}
