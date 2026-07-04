// ============================================================================================
// TEMPORARY PLACEHOLDER DATA — the backend does not model component training cost or upkeep yet.
// The Design Properties panel keeps its Training Cost / Upkeep sections visible (a product decision)
// by sourcing values from these deterministic stand-ins until real fields land on UnitComponent.
//
// To rip this out later: delete this file and the two `placeholder*For` call sites in UnitDesigner /
// ComponentProperty (grep "PLACEHOLDER_COST_UPKEEP"). Nothing else depends on it.
// ============================================================================================

import type { UnitComponent } from "../types/UnitComponent";
import { rawToScaled } from "../types/UnitComponent";

/** Marker string to grep for when removing the placeholder cost/upkeep scaffolding. */
export const PLACEHOLDER_COST_UPKEEP = "PLACEHOLDER_COST_UPKEEP";

/** A resource -> amount map (e.g. { Gold: 100, Iron: 25 }). Mirrors the shape the panels render. */
export type PlaceholderCost = Record<string, number>;

/** The four upkeep categories the panel groups resources under. */
export type PlaceholderUpkeepCategory = "food" | "ammunition" | "fuel" | "repair";

/** Sparse per-category resource maps (e.g. { fuel: { Oil: 2 } }). */
export type PlaceholderUpkeep = Partial<Record<PlaceholderUpkeepCategory, Record<string, number>>>;

/**
 * A small stable pseudo-value derived from the component so different components show different
 * (but repeatable) placeholder numbers. Uses weight as a rough "bigger costs more" signal, nudged by
 * the code so two same-weight components still differ.
 */
function seedFor(component: UnitComponent): number {
  const codeSeed = [...component.code].reduce((sum, char) => sum + char.charCodeAt(0), 0);
  const weightUnits = Math.max(1, Math.round(rawToScaled(component.weight)));
  return weightUnits * 3 + (codeSeed % 7);
}

/** Deterministic placeholder training cost for a component. */
export function placeholderCostFor(component: UnitComponent): PlaceholderCost {
  const seed = seedFor(component);

  // Every component costs some gold; heavier ones also draw an industrial resource that varies by type.
  const cost: PlaceholderCost = { Gold: seed * 10 };
  switch (component.componentType) {
    case "Weapon":
      cost.Iron = seed * 4;
      break;
    case "Armour":
      cost.Iron = seed * 6;
      break;
    case "Engine":
      cost.Oil = seed * 2;
      break;
    case "Body":
      cost.Iron = seed * 3;
      break;
    default:
      break;
  }

  return cost;
}

/** Deterministic placeholder upkeep for a component. */
export function placeholderUpkeepFor(component: UnitComponent): PlaceholderUpkeep {
  const seed = seedFor(component);
  const upkeep: PlaceholderUpkeep = {};

  // Bodies eat food (the crew); weapons consume ammunition; engines burn fuel; everything needs repair.
  if (component.componentType === "Body") {
    upkeep.food = { Rations: Math.max(1, Math.round(seed / 4)) };
  }
  if (component.componentType === "Weapon") {
    upkeep.ammunition = { Shells: Math.max(1, Math.round(seed / 3)) };
  }
  if (component.componentType === "Engine") {
    upkeep.fuel = { Oil: Math.max(1, Math.round(seed / 5)) };
  }
  upkeep.repair = { Parts: Math.max(1, Math.round(seed / 6)) };

  return upkeep;
}
