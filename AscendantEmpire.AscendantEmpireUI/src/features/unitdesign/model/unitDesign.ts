// The assembled unit design (a "template"): a base Body plus the component fitted into each of its slots.
//
// This is the lightweight, serialisable value the designer edits and hands back to its host via onSave.
// It references components by `code` (the canonical identity), indexed positionally to the base body's
// slots, so it stays stable even though the full component catalogue lives elsewhere.

import type { BattleStatsData } from "../BattleStats";
import type { DamageProfile, UnitComponent } from "../types/UnitComponent";
import { ALL_DAMAGE_TYPES, createEmptyDamageProfile } from "../types/UnitComponent";
import {
  placeholderCostFor,
  placeholderUpkeepFor,
  type PlaceholderCost,
  type PlaceholderUpkeep,
  type PlaceholderUpkeepCategory,
} from "./placeholderCosts";

/** An assembled design: the chosen base plus the component fitted into each base slot (by index). */
export interface UnitDesign {
  /** Code of the base Body component, or null when nothing is chosen yet. */
  baseCode: string | null;
  /** Fitted component code per base slot, positionally indexed to the base body's `slots`. */
  slotComponentCodes: (string | null)[];
}

/** A fresh, empty design (no base chosen). */
export function createEmptyDesign(): UnitDesign {
  return { baseCode: null, slotComponentCodes: [] };
}

/** Aggregate non-combat stats for the whole design. All values are raw Scale-1,000 (except movement math). */
export interface DesignSummary {
  /** Total mass: base chassis + every fitted component. */
  weight: number;
  /** Base body power minus the mass of everything fitted (per the unit-design doc's movement formula). */
  movementPoints: number;
  /** The base body's hitpoints (components don't contribute HP). */
  hitpoints: number;
  /** Summed detection contribution across base + fitted components. */
  detection: number;
  /** Summed visibility contribution across base + fitted components. */
  visibility: number;
}

/** Everything the Design Properties panel renders for the assembled design. */
export interface DesignAggregate {
  summary: DesignSummary;
  battleStats: BattleStatsData;
  cost: PlaceholderCost;
  upkeep: PlaceholderUpkeep;
}

/** Adds each entry of `source` into `target` in place (used to merge placeholder cost maps). */
function addInto(target: Record<string, number>, source: Record<string, number>): void {
  for (const [key, amount] of Object.entries(source)) {
    if (!amount) {
      continue;
    }
    target[key] = (target[key] ?? 0) + amount;
  }
}

/**
 * Rolls a base and its fitted components up into the panel's aggregate. `fitted` is the list of
 * components actually slotted (nulls already filtered out by the caller).
 */
export function aggregateDesign(base: UnitComponent | null, fitted: UnitComponent[]): DesignAggregate {
  const all: UnitComponent[] = base ? [base, ...fitted] : [...fitted];

  const attack = createEmptyDamageProfile();
  const resistance = createEmptyDamageProfile();
  const cost: PlaceholderCost = {};
  const upkeep: PlaceholderUpkeep = {};

  let weight = 0;
  let fittedWeight = 0;
  let detection = 0;
  let visibility = 0;

  for (const component of all) {
    weight += component.weight;
    detection += component.detection ?? 0;
    visibility += component.visibility ?? 0;

    // A weapon extension contributes attack; an armour extension contributes resistance. Bodies may
    // carry inherent weapon/armour, so we read the extensions off every component, not just fitted ones.
    if (component.weapon) {
      addProfileInto(attack, component.weapon.attack);
    }
    if (component.armour) {
      addProfileInto(resistance, component.armour.resistance);
    }

    // PLACEHOLDER_COST_UPKEEP: cost/upkeep aren't modelled server-side yet — stand-ins keep the panel populated.
    addInto(cost, placeholderCostFor(component));
    mergeUpkeepInto(upkeep, placeholderUpkeepFor(component));
  }

  for (const component of fitted) {
    fittedWeight += component.weight;
  }

  const summary: DesignSummary = {
    weight,
    movementPoints: (base?.body?.power ?? 0) - fittedWeight,
    hitpoints: base?.body?.hitpoints ?? 0,
    detection,
    visibility,
  };

  return { summary, battleStats: { attack, resistance }, cost, upkeep };
}

/** Adds one damage profile into another in place, across every damage type. */
function addProfileInto(target: DamageProfile, source: DamageProfile): void {
  for (const damageType of ALL_DAMAGE_TYPES) {
    target[damageType] += source[damageType] ?? 0;
  }
}

/** Merges a placeholder upkeep record into an accumulator, summing per category + resource. */
function mergeUpkeepInto(target: PlaceholderUpkeep, source: PlaceholderUpkeep): void {
  for (const [category, resources] of Object.entries(source) as [PlaceholderUpkeepCategory, Record<string, number>][]) {
    if (!resources) {
      continue;
    }
    const categoryTotals = (target[category] ?? {}) as Record<string, number>;
    addInto(categoryTotals, resources);
    target[category] = categoryTotals;
  }
}
