// Editable client-side model for a UnitComponent in the developer Entity Setup Tool.
//
// The shapes here mirror the backend Domain entities / BulkUpsertUnitComponents DTOs
// (AscendantEmpire.Domain.Entities.UnitDesigns.*) so this model can be mapped straight
// onto the upsert payload when persistence is wired up later. For now the tool is UI-only,
// so nothing is sent to the server yet.
//
// Fixed-point note: Weight, Detection, Visibility, every DamageProfile value, the body's CrushAttack,
// and weapon DomainModifier multipliers are all held here as raw Scale-1,000 fixed-point longs
// (1,000 raw = 1.0); the dialog's inputs convert to/from human-friendly decimals at the edit boundary
// (see SCALE / scaled helpers).

import type { DamageType } from "@/features/unitdesign/types/DamageType";
import { ALL_DAMAGE_TYPES } from "@/features/unitdesign/types/UnitComponent";

// The fixed-point scale shared by all Scale-1,000 fields. A raw value of 1,000 represents 1.0.
export const COMPONENT_VALUE_SCALE = 1000;

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
 */
export interface UnitComponentBody {
  movementDomain: DomainType;
  campaignMovementPoints: number;
  battlefieldMovementPoints: number;
  minimumUnitSize: number;
  maximumUnitSize: number;
  sizeIncrement: number;
}

/** Per-Domain damage multiplier for a weapon. Presence of an entry gates which Domains it can attack. */
export type WeaponDomainModifiers = Partial<Record<DomainType, number>>;

/** Weapon-only properties; present only when ComponentType is "Weapon". */
export interface UnitComponentWeapon {
  /** Damage dealt, per type (raw Scale-1,000). */
  attack: DamageProfile;
  minimumRange: number;
  maximumRange: number;
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
 * A slot this component exposes for fitting another component (any component type may carry slots).
 * `displayName` is an optional label. `uiXPosition` / `uiYPosition` are *normalized offsets from the
 * owning component's image center*: (0, 0) is the centre, -1 / +1 are the left/right (X) or top/bottom
 * (Y) edges (relative to the image's half-width / half-height), and values may fall outside -1..1 for
 * slots that sit beyond the image bounds. Nested slots are therefore always relative to their own
 * component's image, so a fitted component's slots compose naturally under the parent's position.
 */
export interface UnitComponentSlot {
  displayName: string | null;
  isVisible: boolean;
  isLocked: boolean;
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
 * A single editable component. `id` is null for a not-yet-saved component (the backend assigns it).
 * A Body populates `body` and may *optionally* also carry `weapon` (inherent attack) and/or `armour`
 * (inherent defence). A Weapon populates only `weapon`; Armour only `armour`. The remaining component
 * types carry none of the three.
 */
export interface UnitComponent {
  id: number | null;
  code: string;
  displayName: string;
  description: string | null;
  /** Optional path to the image shown for this component in the dev Entity Setup Tool. */
  designerImagePath: string | null;
  componentType: ComponentType;
  weight: number;
  /** Raw Scale-1,000; null means the component contributes no detection. */
  detection: number | null;
  /** Raw Scale-1,000; null means the component contributes no visibility. */
  visibility: number | null;
  body: UnitComponentBody | null;
  weapon: UnitComponentWeapon | null;
  armour: UnitComponentArmour | null;
  /** Slots this component exposes for fitting other components (0 or more). Any component type may have them. */
  slots: UnitComponentSlot[];
  /** Codes of buff/classification tags attached to this component (the tag definitions live elsewhere). */
  tagCodes: string[];
  /** Upgrade links where this component is the predecessor — the newer components that supersede it. */
  supersededBy: ComponentSupersessionLink[];
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

/** A fresh body with sensible neutral defaults. */
export function createEmptyBody(): UnitComponentBody {
  return {
    movementDomain: "Land",
    campaignMovementPoints: 0,
    battlefieldMovementPoints: 0,
    minimumUnitSize: 0,
    maximumUnitSize: 0,
    sizeIncrement: 0,
  };
}

/** A fresh weapon with a zeroed attack profile and no targetable Domains. */
export function createEmptyWeapon(): UnitComponentWeapon {
  return {
    attack: createEmptyDamageProfile(),
    minimumRange: 0,
    maximumRange: 1,
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

/** A fresh, visible, unlocked slot at the origin with one starter rule (the backend wants at least one). */
export function createEmptySlot(): UnitComponentSlot {
  return {
    displayName: null,
    isVisible: true,
    isLocked: false,
    uiXPosition: 0,
    uiYPosition: 0,
    rules: [createEmptySlotRule()],
  };
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
    detection: null,
    visibility: null,
    body: createEmptyBody(),
    weapon: null,
    armour: null,
    slots: [],
    tagCodes: [],
    supersededBy: [],
  };
}
