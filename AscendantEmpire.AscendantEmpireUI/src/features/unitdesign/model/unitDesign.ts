// The assembled unit design (a "template"): a base Body plus the component fitted into each of its slots.
//
// This is the lightweight, serialisable value the designer edits and hands back to its host via onSave.
// It references components by `code` (the canonical identity).
//
// Fittings are held positionally here, index-aligned with the base body's `slots` array the designer is
// rendering from. That is safe only because it is transient working state, rebuilt from the current slot
// list every time a design is loaded. Nothing positional is ever persisted or sent: the wire contract keys
// fittings by the slot's own `code` (see UnitDesigner's save handler), so changing a body's slots cannot
// silently re-map which component sits where.

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
import { calculateBattleMovementPoints } from "./battleMovement";

/** An assembled design: the chosen base plus the component fitted into each base slot (by index). */
export interface UnitDesign {
  /** Code of the base Body component, or null when nothing is chosen yet. */
  baseCode: string | null;
  /**
   * Fitted component code per base slot, index-aligned with the base body's `slots`. Working state only —
   * see the file header: it is converted to a slot-code map before anything leaves the designer.
   */
  slotComponentCodes: (string | null)[];
}

/** A fresh, empty design (no base chosen). */
export function createEmptyDesign(): UnitDesign {
  return { baseCode: null, slotComponentCodes: [] };
}

/** Aggregate non-combat stats for the whole design. All values are raw Scale-1,000. */
export interface DesignSummary {
  /** Total mass: base chassis + every fitted component. */
  weight: number;
  /**
   * Tiles the design can cross in one battle turn: base body power minus the mass of everything fitted,
   * floored at zero. The base's own weight does not count against it — see `calculateBattleMovementPoints`.
   */
  battleMovementPoints: number;
  /** The base body's hitpoints (components don't contribute HP). */
  hitpoints: number;
  /** Summed sight contribution across base + fitted components — the design's campaign-map reveal radius. */
  sight: number;
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
  let sight = 0;
  let detection = 0;
  let visibility = 0;

  for (const component of all) {
    weight += component.weight;
    // Mirrors the server's compile step: null contributes nothing rather than defaulting to a value.
    sight += component.sight ?? 0;
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
    // Only `fittedWeight` is subtracted, never `weight` — the latter includes the base's own mass, which
    // power already accounts for.
    battleMovementPoints: calculateBattleMovementPoints(base?.body?.power ?? 0, fittedWeight),
    hitpoints: base?.body?.hitpoints ?? 0,
    sight,
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
