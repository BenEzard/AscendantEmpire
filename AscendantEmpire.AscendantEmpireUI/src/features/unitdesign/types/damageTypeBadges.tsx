// Shared icon/label metadata for the five damage types.
//
// Extracted from BattleStats so the read-only battle summary and the editable Unit Designer pill editor
// render the same glyph for each damage type (one source of truth). Icons come from the react-icons
// "gi" (game-icons) set, matching the rest of the unit-design UI.

import type { IconType } from "react-icons";
import { GiFlatHammer, GiFlame, GiPoisonBottle, GiQuickSlash, GiSunSpear } from "react-icons/gi";

import type { DamageType } from "./DamageType";

export interface DamageTypeBadge {
  /** Human-readable name, used for tooltips/labels. */
  title: string;
  /** Glyph representing the damage type. */
  Icon: IconType;
}

/** One icon per damage type. Keyed over DamageType so the compiler flags any missing/added type. */
export const DAMAGE_TYPE_ICONS: Record<DamageType, IconType> = {
  crush: GiFlatHammer,
  pierce: GiSunSpear,
  burn: GiFlame,
  slash: GiQuickSlash,
  poison: GiPoisonBottle,
};

/** Icon + title per damage type (the title doubles as the tooltip text). */
export const DAMAGE_TYPE_BADGES: Record<DamageType, DamageTypeBadge> = {
  crush: { title: "Crush", Icon: DAMAGE_TYPE_ICONS.crush },
  pierce: { title: "Pierce", Icon: DAMAGE_TYPE_ICONS.pierce },
  burn: { title: "Burn", Icon: DAMAGE_TYPE_ICONS.burn },
  slash: { title: "Slash", Icon: DAMAGE_TYPE_ICONS.slash },
  poison: { title: "Poison", Icon: DAMAGE_TYPE_ICONS.poison },
};
