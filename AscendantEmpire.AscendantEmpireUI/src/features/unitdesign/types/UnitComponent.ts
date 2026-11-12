// Canonical client-side model for a UnitComponent. This is the single source of truth for the unit
// design domain, shared by the in-game designer (features/unitdesign) and the developer Entity Setup
// Tool (features/entitysetuptool).
//
// The shapes here mirror the backend Domain entities / BulkUpsertUnitComponents DTOs
// (AscendantEmpire.Domain.Entities.UnitDesigns.*) so this model can be mapped straight onto the upsert
// payload (see features/unitdesign/api/componentsApi.ts).
//
// Fixed-point note: Weight, Detection, Visibility, every DamageProfile value, the body's movement/power/
// hitpoints, and weapon DomainModifier multipliers are all held here as raw Scale-1,000 fixed-point longs
// (1,000 raw = 1.0); editors convert to/from human-friendly decimals at the edit boundary
// (see ScaledNumberField's SCALE / scaled helpers).

import { toCodeSlug } from "@/util/codeSlug";

import type { DamageType } from "./DamageType";

/**
 * The five damage types in canonical order. Homed here (rather than DamageType.ts) because it is the
 * order every DamageProfile is iterated in; imported across both features.
 */
export const ALL_DAMAGE_TYPES: readonly DamageType[] = ["crush", "pierce", "burn", "slash", "poison"] as const;

// The fixed-point scale shared by all Scale-1,000 fields. A raw value of 1,000 represents 1.0.
export const COMPONENT_VALUE_SCALE = 1000;

/** Raw fixed-point long -> display decimal (e.g. 1500 -> 1.5). */
export function rawToScaled(raw: number): number {
  return raw / COMPONENT_VALUE_SCALE;
}

/** Display decimal -> raw fixed-point long (e.g. 1.5 -> 1500), rounded to the nearest whole unit. */
export function scaledToRaw(scaled: number): number {
  return Math.round(scaled * COMPONENT_VALUE_SCALE);
}

/**
 * The category a UnitComponent belongs to. Names match the C# `ComponentType` enum exactly so the
 * value round-trips to the backend without translation.
 */
export type ComponentType = "Body" | "Weapon" | "Engine" | "Armour" | "Equipment" | "Training" | "Extension";

// Order follows the backend enum (minus the types not surfaced by this tool) so dropdowns read the
// same as the design doc.
export const COMPONENT_TYPES: readonly ComponentType[] = ["Body", "Weapon", "Engine", "Armour", "Equipment", "Training", "Extension"];

// The component types a slot's "ComponentType" acceptance rule may match against — every type except
// Body, since a Body is a Unit Template's root and is never itself fitted into another component's
// slot (see SlotRuleEvaluator on the backend). Sorted alphabetically rather than mirroring the backend
// enum order, since this list is only ever shown as a rule-matching option list, not a type selector.
export const SLOT_MATCHABLE_COMPONENT_TYPES: readonly ComponentType[] = COMPONENT_TYPES.filter((type) => type !== "Body")
  .slice()
  .sort((a, b) => a.localeCompare(b));

/**
 * The Domain a body moves in / a weapon can engage. Names match the C# `DomainType` enum
 * (Maps namespace) exactly.
 */
export type DomainType = "Land" | "Water" | "BelowWater" | "Air" | "NearEarthOrbit";

export const DOMAIN_TYPES: readonly DomainType[] = ["Land", "Water", "BelowWater", "Air", "NearEarthOrbit"];

/**
 * Which attribute of a candidate component a slot acceptance rule matches against. Names match the C#
 * `SlotRuleType` enum exactly. (Weight/subtype rules are deferred backend-side, so only these two exist.)
 */
export type SlotRuleType = "ComponentType" | "HasTag";

export const SLOT_RULE_TYPES: readonly SlotRuleType[] = ["ComponentType", "HasTag"];

/**
 * How a slot acceptance rule compares its value against the candidate. Names match the C#
 * `SlotRuleOperator` enum exactly. `In` treats the rule value as a comma-separated list.
 */
export type SlotRuleOperator = "Equals" | "NotEquals" | "In";

export const SLOT_RULE_OPERATORS: readonly SlotRuleOperator[] = ["Equals", "NotEquals", "In"];

/**
 * Damage dealt (weapon) or mitigated (armour), split across the five damage types. Each value is a
 * raw Scale-1,000 fixed-point integer. Keyed by the lowercase DamageType union used elsewhere on the
 * client; maps onto the PascalCase columns of the backend DamageProfile.
 */
export type DamageProfile = Record<DamageType, number>;

/**
 * Body-only properties; present only when ComponentType is "Body". A body's inherent attack/defence
 * (if any) live in the component's optional `weapon`/`armour` extensions, not here.
 *
 * `campaignMovementPoints`, `battlefieldMovementPoints`, `power` and `hitpoints` are all raw Scale-1,000
 * fixed-point values (1,000 raw = 1.0); edit them through ScaledNumberField so the user sees decimals.
 *
 * Only bodies expose Template Slots (`slots`) — a Unit Template is built around a body and every other
 * component is fitted into one of its slots.
 */
export interface UnitComponentBody {
  movementDomain: DomainType;
  campaignMovementPoints: number;
  battlefieldMovementPoints: number;
  power: number;
  hitpoints: number;
  /** Slots this body exposes for fitting other components (0 or more). */
  slots: UnitComponentSlot[];
}

/** Per-Domain damage multiplier for a weapon. Presence of an entry gates which Domains it can attack. */
export type WeaponDomainModifiers = Partial<Record<DomainType, number>>;

/** Weapon-only properties; present only when ComponentType is "Weapon". */
export interface UnitComponentWeapon {
  /** Damage dealt, per type (raw Scale-1,000). */
  attack: DamageProfile;
  minimumRange: number;
  maximumRange: number;
  /** Whether this weapon is wielded two at a time (e.g. dual pistols). */
  dualWield: boolean;
  /** Target Domains this weapon can engage, each with a raw Scale-1,000 damage multiplier (1,000 = 100%). */
  domainModifiers: WeaponDomainModifiers;
}

/** Armour-only properties; present only when ComponentType is "Armour". */
export interface UnitComponentArmour {
  /** Flat damage reduction, per type (raw Scale-1,000). */
  resistance: DamageProfile;
}

/**
 * A single acceptance rule for a slot. A candidate component must satisfy all of a slot's rules to be
 * fitted. `value` is interpreted per `ruleType`: a ComponentType name (or comma-separated list when the
 * operator is `In`) for "ComponentType", or a tag code for "HasTag".
 */
export interface UnitComponentSlotRule {
  ruleType: SlotRuleType;
  operator: SlotRuleOperator;
  value: string;
}

/**
 * A slot a body exposes for fitting another component (only bodies carry slots). `displayName` is the
 * slot's label and the source its `code` is derived from, so it is required.
 * `uiXPosition` / `uiYPosition` are *normalized offsets from the body's image center*:
 * (0, 0) is the centre, -1 / +1 are the left/right (X) or top/bottom (Y) edges (relative to the image's
 * half-width / half-height), and values may fall outside -1..1 for slots that sit beyond the image bounds.
 */
export interface UnitComponentSlot {
  /**
   * The slot's database id. Present when read from the backend; absent for a freshly-authored slot. Sent on
   * save so the backend updates this exact row in place — which is also the only way it can tell a `code`
   * rename apart from a slot being removed and a different one added.
   */
  id?: number;
  /**
   * Stable key for the slot, unique within its body (e.g. `main-hand`). This is how a template names the slot
   * it fits a component into, and how a catalogue imported from another database matches slots to existing
   * rows — neither of which an id or a position can do.
   *
   * Derived from `displayName` via `toSlotCode`, never typed by hand — which is why two slots on one body
   * may not share a name, and why the editor shows this as a tooltip rather than a field.
   */
  code: string;
  /** The slot's label, and the source `code` is derived from. Required — a blank name means a blank code. */
  displayName: string;
  isVisible: boolean;
  uiXPosition: number;
  uiYPosition: number;
  /** Acceptance rules (a candidate must satisfy all of them). The backend expects at least one. */
  rules: UnitComponentSlotRule[];
}

/**
 * A directed upgrade link in which THIS component is the predecessor — i.e. a newer component that
 * supersedes it. Mirrors the backend ComponentSupersession (keyed by code); the editing component is
 * always the predecessor, so we only store the successor side here.
 */
export interface ComponentSupersessionLink {
  /** Code of the newer component that supersedes this one. */
  successorCode: string;
  /** Whether this is the recommended/default upgrade. At most one link should be flagged. */
  isRecommended: boolean;
}

/**
 * A single component. `id` is null for a not-yet-saved component (the backend assigns it).
 * A Body populates `body` and may *optionally* also carry `weapon` (inherent attack) and/or `armour`
 * (inherent defence). A Weapon populates only `weapon`; Armour only `armour`. The remaining component
 * types carry none of the three.
 */
export interface UnitComponent {
  id: number | null;
  code: string;
  displayName: string;
  description: string | null;
  /** Optional path to the image shown for this component in the designer / dev Entity Setup Tool. */
  designerImagePath: string | null;
  componentType: ComponentType;
  weight: number;
  /** Raw Scale-1,000; null means the component contributes no sight. Summed across a design to give its map reveal radius. */
  sight: number | null;
  /** Raw Scale-1,000; null means the component contributes no detection. */
  detection: number | null;
  /** Raw Scale-1,000; null means the component contributes no visibility. */
  visibility: number | null;
  body: UnitComponentBody | null;
  weapon: UnitComponentWeapon | null;
  armour: UnitComponentArmour | null;
  /** Codes of buff/classification tags attached to this component (the tag definitions live elsewhere). */
  tagCodes: string[];
  /** Upgrade links where this component is the predecessor — the newer components that supersede it. */
  supersededBy: ComponentSupersessionLink[];
  /** What this component consumes, as quantities of supply sets. Only a Body may carry any. */
  supplyRequirements: UnitComponentSupplyRequirement[];
}

/**
 * One ongoing need a component has, as a quantity of one supply set.
 */
/* What the quantity is measured per comes from the set's supplyType — per troop for Food, per movement
   point for Fuel, per damage point for Repair — so there is no basis to carry here. */
export interface UnitComponentSupplyRequirement {
  /** Code of the supply set the need draws on; must resolve to a set in the catalogue. */
  supplySetCode: string;
  /** Raw Scale-1,000 — 1,000 is one unit of the need. */
  quantity: number;
}

/** A zeroed DamageProfile — used when first switching a component to weapon/armour. */
export function createEmptyDamageProfile(): DamageProfile {
  return { crush: 0, pierce: 0, burn: 0, slash: 0, poison: 0 };
}

/**
 * True when a damage profile carries at least one positive value. Used to decide whether a body's
 * optional inherent attack/defence is worth keeping — an all-zero profile is treated as "none".
 */
export function damageProfileHasPositiveValue(profile: DamageProfile): boolean {
  return ALL_DAMAGE_TYPES.some((damageType) => profile[damageType] > 0);
}

/** A fresh body with sensible neutral defaults (and no slots yet). */
export function createEmptyBody(): UnitComponentBody {
  return {
    movementDomain: "Land",
    campaignMovementPoints: 0,
    battlefieldMovementPoints: 0,
    power: 0,
    hitpoints: 0,
    slots: [],
  };
}

/** A fresh weapon with a zeroed attack profile and no targetable Domains. */
export function createEmptyWeapon(): UnitComponentWeapon {
  return {
    attack: createEmptyDamageProfile(),
    minimumRange: 0,
    maximumRange: 0,
    dualWield: false,
    domainModifiers: {},
  };
}

/** A fresh armour with a zeroed resistance profile. */
export function createEmptyArmour(): UnitComponentArmour {
  return { resistance: createEmptyDamageProfile() };
}

/** A fresh slot rule defaulting to the simplest case: matches on a single ComponentType. */
export function createEmptySlotRule(): UnitComponentSlotRule {
  return { ruleType: "ComponentType", operator: "Equals", value: "" };
}

/**
 * Derives a slot's code from its display name, using the same rule as component and tag codes
 * (`toCodeSlug`): lowercase, every non-alphanumeric character to a hyphen, ends trimmed.
 *
 * Wrapped rather than called directly so the slot-specific reason for it stays findable: the code is the
 * slot's identity, so this is the single point where a name becomes one.
 */
export function toSlotCode(displayName: string): string {
  return toCodeSlug(displayName);
}

/**
 * A fresh slot at the origin with one starter rule (the backend wants at least one). Defaults to visible.
 * The code is derived from `displayName`, which must be unique within the body — see `nextSlotName`, which
 * the Slots editor uses to pick a starting one.
 */
export function createEmptySlot(displayName: string): UnitComponentSlot {
  return {
    code: toSlotCode(displayName),
    displayName,
    isVisible: true,
    uiXPosition: 0,
    uiYPosition: 0,
    rules: [createEmptySlotRule()],
  };
}

/**
 * Picks an unused `Slot n` display name for a body that already exposes `slots`, which in turn derives a
 * `slot-n` code.
 *
 * A generated placeholder rather than a prompt: the dev renames it to something meaningful in the editor,
 * and because the slot's id travels with the rename, doing so updates the row rather than replacing it.
 * Names are compared case-insensitively, since two names differing only in case would still collapse to the
 * same code.
 */
export function nextSlotName(slots: readonly UnitComponentSlot[]): string {
  const taken = new Set(slots.map((slot) => slot.displayName.trim().toLowerCase()));

  for (let candidate = slots.length + 1; ; candidate++) {
    const displayName = `Slot ${candidate}`;
    if (!taken.has(displayName.toLowerCase())) {
      return displayName;
    }
  }
}

// ---------------------------------------------------------------------------
// Table-editor row mapping
//
// The Component Setup dialog edits damage profiles / domain modifiers as a sparse, user-grown list of
// rows (one row per key the user actually adds) to save vertical space. The model itself is unchanged:
// these helpers translate between that row list and the hydrated DamageProfile / WeaponDomainModifiers
// when the dialog opens (seed rows) and commits (rebuild the profile/record).
// ---------------------------------------------------------------------------

/** One editable row in a key/value table: an enum key paired with its raw Scale-1,000 value. */
export interface KeyValueRow<TKey extends string> {
  key: TKey;
  value: number;
}

/**
 * DamageProfile -> rows. Emits one row per damage type whose value is non-zero, in the canonical
 * ALL_DAMAGE_TYPES order. Zero entries are dropped so the editor starts compact.
 */
export function damageProfileToRows(profile: DamageProfile): KeyValueRow<DamageType>[] {
  return ALL_DAMAGE_TYPES.filter((damageType) => profile[damageType] !== 0).map((damageType) => ({
    key: damageType,
    value: profile[damageType],
  }));
}

/**
 * Rows -> hydrated DamageProfile. Starts from a fully zeroed profile and writes each row's value, so
 * every damage type is present (missing types stay 0). Later rows for the same key win (shouldn't
 * happen — the editor enforces one row per key).
 */
export function rowsToDamageProfile(rows: KeyValueRow<DamageType>[]): DamageProfile {
  const profile = createEmptyDamageProfile();
  for (const row of rows) {
    profile[row.key] = row.value;
  }

  return profile;
}

/**
 * WeaponDomainModifiers -> rows. Emits one row per domain that has an entry, in the canonical
 * DOMAIN_TYPES order. Domains absent from the record (the weapon can't engage them) produce no row.
 */
export function domainModifiersToRows(modifiers: WeaponDomainModifiers): KeyValueRow<DomainType>[] {
  return DOMAIN_TYPES.filter((domain) => modifiers[domain] != null).map((domain) => ({
    key: domain,
    value: modifiers[domain] as number,
  }));
}

/**
 * Rows -> WeaponDomainModifiers. Builds a sparse record from the rows only; domains without a row are
 * left absent (the weapon cannot attack them).
 */
export function rowsToDomainModifiers(rows: KeyValueRow<DomainType>[]): WeaponDomainModifiers {
  const modifiers: WeaponDomainModifiers = {};
  for (const row of rows) {
    modifiers[row.key] = row.value;
  }

  return modifiers;
}

/** A blank component for the "Add" flow — defaults to a Body since every template needs one. */
export function createEmptyComponent(): UnitComponent {
  return {
    id: null,
    code: "",
    displayName: "",
    description: null,
    designerImagePath: null,
    componentType: "Body",
    weight: 0,
    sight: null,
    detection: null,
    visibility: null,
    body: createEmptyBody(),
    weapon: null,
    armour: null,
    tagCodes: [],
    supersededBy: [],
    supplyRequirements: [],
  };
}
