// Maps each UnitComponent ComponentType to a react-icons component, so the Component Setup dialog can
// render the type as an icon (the type→icon relationship lives here rather than in the data model,
// since UnitComponent carries no icon field). Mirrors the iconMap pattern in
// features/map/components/mapEditorIcons.ts.

import type { IconType } from "react-icons";
import { GiBackpack, GiCrossedSwords, GiGearHammer, GiGears, GiPerson, GiShield, GiWeightLiftingUp } from "react-icons/gi";

import type { ComponentType } from "@/features/unitdesign/types/UnitComponent";

// One icon per component type. Keyed over ComponentType so the compiler flags any missing/added type.
export const componentTypeIcons: Record<ComponentType, IconType> = {
  Body: GiPerson,
  Weapon: GiCrossedSwords,
  Engine: GiGears,
  Armour: GiShield,
  Equipment: GiBackpack,
  Training: GiWeightLiftingUp,
  Extension: GiGearHammer,
};
