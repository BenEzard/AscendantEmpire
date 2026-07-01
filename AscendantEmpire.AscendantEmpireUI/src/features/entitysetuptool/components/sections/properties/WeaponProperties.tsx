// WeaponProperties — the Properties-tab editor shown when the component is a Weapon.
//
// Controlled component, but it owns its own working-row state for the two sparse editors (Attack and
// Target Domains). The rows are kept separate from the model so a row left at 0 doesn't vanish
// mid-edit; every change rebuilds a fully-hydrated weapon (attack + domainModifiers) and emits it via
// onChange, so the parent's model is always in sync and never has to know about rows.
//
// The rows are seeded once from the incoming weapon on mount. The dialog remounts this component on
// each open (via an open-instance key), so reopening re-seeds from a freshly-reset weapon.

import { useState } from "react";

import type { DamageType } from "@/features/unitdesign/types/DamageType";
import { ALL_DAMAGE_TYPES } from "@/features/unitdesign/types/UnitComponent";
import { DAMAGE_TYPE_ICONS } from "@/features/unitdesign/types/damageTypeBadges";

import {
  COMPONENT_VALUE_SCALE,
  DOMAIN_TYPES,
  damageProfileToRows,
  domainModifiersToRows,
  rowsToDamageProfile,
  rowsToDomainModifiers,
  type DomainType,
  type KeyValueRow,
  type UnitComponentWeapon,
} from "../../../types/UnitComponent";
import { RangeSlider } from "@/components/ui";

import PillKeyValueEditor from "../../PillKeyValueEditor";
import { DAMAGE_TYPE_LABELS, DOMAIN_TYPE_LABELS } from "../../componentSetupLabels";

interface WeaponPropertiesProps {
  weapon: UnitComponentWeapon;
  onChange: (next: UnitComponentWeapon) => void;
}

export default function WeaponProperties({ weapon, onChange }: WeaponPropertiesProps) {
  // Working rows for the sparse editors, seeded once from the incoming weapon.
  const [attackRows, setAttackRows] = useState<KeyValueRow<DamageType>[]>(() => damageProfileToRows(weapon.attack));
  const [domainRows, setDomainRows] = useState<KeyValueRow<DomainType>[]>(() => domainModifiersToRows(weapon.domainModifiers));

  // Rebuild and emit a fully-hydrated weapon from the latest pieces. Whichever part just changed is
  // passed as an override, because the matching setState above hasn't flushed yet when we emit.
  const emit = (overrides: {
    base?: UnitComponentWeapon;
    attack?: KeyValueRow<DamageType>[];
    domains?: KeyValueRow<DomainType>[];
  }) => {
    const base = overrides.base ?? weapon;
    const nextAttackRows = overrides.attack ?? attackRows;
    const nextDomainRows = overrides.domains ?? domainRows;
    onChange({
      ...base,
      attack: rowsToDamageProfile(nextAttackRows),
      domainModifiers: rowsToDomainModifiers(nextDomainRows),
    });
  };

  const handleAttackChange = (next: KeyValueRow<DamageType>[]) => {
    setAttackRows(next);
    emit({ attack: next });
  };

  const handleDomainChange = (next: KeyValueRow<DomainType>[]) => {
    setDomainRows(next);
    emit({ domains: next });
  };

  // The Range slider edits both ends of the weapon's range at once; emit through the same helper so
  // attack/domains stay hydrated.
  const handleRangeChange = (next: { min: number; max: number }) => {
    emit({ base: { ...weapon, minimumRange: next.min, maximumRange: next.max } });
  };

  return (
    <div className="w-full max-w-xl">
      <div className="mb-1 text-sm font-semibold text-slate-700">Attack</div>
      <PillKeyValueEditor
        rows={attackRows}
        onChange={handleAttackChange}
        allKeys={ALL_DAMAGE_TYPES}
        keyLabels={DAMAGE_TYPE_LABELS}
        keyIcons={DAMAGE_TYPE_ICONS}
        keyHeader="Damage Type"
        valueHeader="Damage Value"
        defaultValueRaw={0}
      />

      <div className="mt-3">
        {/* Single dual-knob slider in place of the old Minimum/Maximum Range number fields. */}
        <RangeSlider
          label="Range"
          minValue={weapon.minimumRange}
          maxValue={weapon.maximumRange}
          trackMin={0}
          trackMax={10}
          step={1}
          onChange={handleRangeChange}
        />
      </div>

      {/* Domain modifiers: each row is a Domain the weapon can engage, with its damage multiplier. */}
      <div className="mt-3 mb-1 text-sm font-semibold text-slate-700">Target Domains</div>
      <PillKeyValueEditor
        rows={domainRows}
        onChange={handleDomainChange}
        allKeys={DOMAIN_TYPES}
        keyLabels={DOMAIN_TYPE_LABELS}
        // No domain icons yet, so domain pills fall back to their text label.
        keyHeader="Target Domain"
        valueHeader="Multiplier"
        // A freshly added Domain defaults to 100% damage (raw 1,000), matching the old checkbox behaviour.
        defaultValueRaw={COMPONENT_VALUE_SCALE}
      />
      <p className="mt-1 text-xs text-slate-400">Multiplier 1.0 = 100% damage. Domains not listed cannot be attacked.</p>
    </div>
  );
}
