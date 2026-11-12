// Maps each SupplyType to a react-icons component, so the Supply Sets dialog can render the type as an
// icon rather than a dropdown. The type→icon relationship lives here rather than in the data model,
// since a SupplySet carries no icon field. Mirrors componentTypeIcons, which does the same for the
// Component Setup dialog's Component Type.

import type { IconType } from "react-icons";
import { GiAutoRepair, GiFuelTank, GiMeal } from "react-icons/gi";

import type { SupplyType } from "../api/supplySetApi";

// Keyed over SupplyType so the compiler flags any missing or added type.
export const supplyTypeIcons: Record<SupplyType, IconType> = {
  Food: GiMeal,
  Fuel: GiFuelTank,
  Repair: GiAutoRepair,
};
