// Human-readable labels for the enum keys edited across the Component Setup sections. Lifted out of
// ComponentSetupDialog so the per-type Properties editors can share a single source of truth.

import type { DamageType } from "@/features/unitdesign/types/DamageType";

import type { DomainType } from "../types/UnitComponent";

// Labels for the five damage types, in the canonical ALL_DAMAGE_TYPES order.
export const DAMAGE_TYPE_LABELS: Record<DamageType, string> = {
  crush: "Crush",
  pierce: "Pierce",
  burn: "Burn",
  slash: "Slash",
  poison: "Poison",
};

// Labels for the movement / target domains (the multi-word ones get a friendlier display form).
export const DOMAIN_TYPE_LABELS: Record<DomainType, string> = {
  Land: "Land",
  Water: "Water",
  BelowWater: "Below Water",
  Air: "Air",
  NearEarthOrbit: "Near-Earth Orbit",
};
