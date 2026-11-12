// AttackSection — the "Attack (Body)" tab. A Body may optionally have an inherent attack: its own
// weapon, edited with the same WeaponProperties editor used by Weapon components. The tab is only
// enabled for Body components (see ComponentSetupDialog), so it renders nothing for any other type.
//
// There's no opt-in toggle: the editor is always shown for a Body, seeded from an empty weapon until
// the user enters values. The weapon is dropped at save time when its attack has no positive value
// (see ComponentSetupDialog's handleCommit), so a body without an inherent attack stays lean.

import { createEmptyWeapon, type UnitComponent } from "@/features/unitdesign/types/UnitComponent";

import WeaponProperties from "./properties/WeaponProperties";
import type { AssetOption } from "../../api/assetApi";

interface AttackSectionProps {
  component: UnitComponent;
  onChange: (next: UnitComponent) => void;
  /** The Asset catalogue the inherent attack's ammunition editor offers. */
  assets: AssetOption[];
  assetsLoading?: boolean;
  assetsError?: string | null;
}

export default function AttackSection({ component, onChange, assets, assetsLoading, assetsError }: AttackSectionProps) {
  // The tab is disabled for non-Body types; guard so its (unreachable) content stays inert.
  if (component.componentType !== "Body") {
    return null;
  }

  // Seed the editor from an empty weapon when the body has none yet; it's only written to the model
  // once the user edits a value.
  const weapon = component.weapon ?? createEmptyWeapon();

  return (
    <div className="w-full max-w-xl">
      <p className="mb-3 text-xs text-slate-400">Optional attack the body deals itself. Leave the attack empty for no inherent attack.</p>
      <WeaponProperties
        weapon={weapon}
        onChange={(next) => onChange({ ...component, weapon: next })}
        assets={assets}
        assetsLoading={assetsLoading}
        assetsError={assetsError}
      />
    </div>
  );
}
