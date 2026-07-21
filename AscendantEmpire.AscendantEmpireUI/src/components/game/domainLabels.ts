// Human-readable labels for the DomainType enum, in canonical DOMAIN_TYPES order. Homed in the shared
// game-components layer (rather than inside any one feature) so both the shared DomainSelection control
// and the feature-level editors can share a single source of truth. The multi-word domains get a
// friendlier display form.

import type { DomainType } from "@/features/unitdesign/types/UnitComponent";

export const DOMAIN_TYPE_LABELS: Record<DomainType, string> = {
  Land: "Land",
  Water: "Water",
  BelowWater: "Below Water",
  Air: "Air",
  NearEarthOrbit: "Near-Earth Orbit",
};
