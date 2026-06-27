// DefenceSection — the "Defence (Body)" tab. A Body may optionally have inherent defence: its own
// armour, edited with the same ArmourProperties editor used by Armour components. The tab is only
// enabled for Body components (see ComponentSetupDialog), so it renders nothing for any other type.
//
// There's no opt-in toggle: the editor is always shown for a Body, seeded from an empty armour until
// the user enters values. The armour is dropped at save time when its resistance has no positive value
// (see ComponentSetupDialog's handleCommit), so a body without inherent defence stays lean.

import { createEmptyArmour, type UnitComponent } from "../../types/UnitComponent";

import ArmourProperties from "./properties/ArmourProperties";

interface DefenceSectionProps {
  component: UnitComponent;
  onChange: (next: UnitComponent) => void;
}

export default function DefenceSection({ component, onChange }: DefenceSectionProps) {
  // The tab is disabled for non-Body types; guard so its (unreachable) content stays inert.
  if (component.componentType !== "Body") {
    return null;
  }

  // Seed the editor from an empty armour when the body has none yet; it's only written to the model
  // once the user edits a value.
  const armour = component.armour ?? createEmptyArmour();

  return (
    <div className="w-full max-w-xl">
      <p className="mb-3 text-xs text-slate-400">Optional defence the body provides itself. Leave the resistance empty for no inherent defence.</p>
      <ArmourProperties armour={armour} onChange={(next) => onChange({ ...component, armour: next })} />
    </div>
  );
}
