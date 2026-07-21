// Human-readable labels for the enum keys edited across the Component Setup sections. Lifted out of
// ComponentSetupDialog so the per-type Properties editors can share a single source of truth.

import type { DamageType } from "@/features/unitdesign/types/DamageType";

// The domain labels are game-domain and shared, so they live in the shared game-components layer. Kept
// re-exported here so the existing componentSetupLabels import sites (e.g. WeaponProperties) are unchanged.
export { DOMAIN_TYPE_LABELS } from "@/components/game/domainLabels";

// Labels for the five damage types, in the canonical ALL_DAMAGE_TYPES order.
export const DAMAGE_TYPE_LABELS: Record<DamageType, string> = {
  crush: "Crush",
  pierce: "Pierce",
  burn: "Burn",
  slash: "Slash",
  poison: "Poison",
};
