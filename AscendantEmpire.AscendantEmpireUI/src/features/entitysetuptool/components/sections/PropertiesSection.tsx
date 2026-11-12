// PropertiesSection — the dialog's "Properties" page. It picks the right per-type editor for the
// component's current type and maps that editor's slice-level onChange back onto the whole component.
//
// Note on preservation: the orchestrator keeps every extension (body/weapon/armour) populated in state
// while the dialog is open, even for types that aren't currently active, so toggling type never loses
// in-progress edits. This section only ever renders the editor for the *active* type; the non-active
// extensions ride along untouched until they're collapsed away at Save time.

import type { UnitComponent } from "@/features/unitdesign/types/UnitComponent";

import ArmourProperties from "./properties/ArmourProperties";
import BodyProperties from "./properties/BodyProperties";
import WeaponProperties from "./properties/WeaponProperties";
import type { AssetOption } from "../../api/assetApi";

interface PropertiesSectionProps {
  component: UnitComponent;
  onChange: (next: UnitComponent) => void;
  /** The Asset catalogue a weapon's ammunition editor offers. */
  assets: AssetOption[];
  assetsLoading?: boolean;
  assetsError?: string | null;
}

export default function PropertiesSection({ component, onChange, assets, assetsLoading, assetsError }: PropertiesSectionProps) {
  if (component.componentType === "Body" && component.body) {
    // The body's movement/strength fields. Its optional inherent attack/defence live on "More Properties".
    return <BodyProperties body={component.body} onChange={(body) => onChange({ ...component, body })} />;
  }

  if (component.componentType === "Weapon" && component.weapon) {
    return (
      <WeaponProperties
        weapon={component.weapon}
        onChange={(weapon) => onChange({ ...component, weapon })}
        assets={assets}
        assetsLoading={assetsLoading}
        assetsError={assetsError}
      />
    );
  }

  if (component.componentType === "Armour" && component.armour) {
    return <ArmourProperties armour={component.armour} onChange={(armour) => onChange({ ...component, armour })} />;
  }

  // Engine, Equipment, Training, Extension, Radar, System, Fitting, Special — no type-specific properties yet.
  return (
    <div className="w-full max-w-xl text-sm text-slate-500">
      <p>The “{component.componentType}” component type has no additional properties.</p>
    </div>
  );
}
