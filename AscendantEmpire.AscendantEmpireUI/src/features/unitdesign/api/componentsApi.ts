// API service for the dev Entity Setup Tool's component catalog. Thin wrapper over the shared axios
// `http` client for the BulkUpsertUnitComponents endpoint (POST /unit-design/components/bulk).

import { http } from "@/api/http";

import type {
  ComponentSupersessionLink,
  ComponentType,
  DamageProfile,
  DomainType,
  UnitComponent,
  UnitComponentArmour,
  UnitComponentBody,
  UnitComponentSlot,
  UnitComponentSlotRule,
  UnitComponentWeapon,
  WeaponDomainModifiers,
} from "../types/UnitComponent";

/** Shape returned by POST /unit-design/components/bulk (mirrors IBulkUpsertUnitComponentsResult). */
export interface BulkUpsertComponentsResult {
  addedCount: number;
  updatedCount: number;
  updatedCodes: string[];
  /** How many body slots the save deleted because they were removed from the payload. */
  removedSlotCount: number;
  /**
   * Templates that lost a fitted component because the slot holding it was deleted. Deleting a slot takes its
   * fittings with it, so this is reported back rather than being discovered later in the designer.
   */
  unfittedTemplateNames: string[];
}

// --- internal payload mappers ---

function mapDamageProfile(profile: DamageProfile) {
  return {
    crush: profile.crush,
    pierce: profile.pierce,
    burn: profile.burn,
    slash: profile.slash,
    poison: profile.poison,
  };
}

function mapBody(body: UnitComponentBody) {
  return {
    movementDomain: body.movementDomain,
    // Movement/power/hitpoints are raw Scale-1,000 longs, sent through as-is.
    campaignMovementPoints: body.campaignMovementPoints,
    battlefieldMovementPoints: body.battlefieldMovementPoints,
    power: body.power,
    hitpoints: body.hitpoints,
    // Only bodies carry slots, so they ride on the body payload.
    slots: body.slots.map(mapSlot),
  };
}

function mapWeapon(weapon: UnitComponentWeapon) {
  return {
    attack: mapDamageProfile(weapon.attack),
    minimumRange: weapon.minimumRange,
    maximumRange: weapon.maximumRange,
    dualWield: weapon.dualWield,
    // WeaponDomainModifiers is a sparse record keyed by DomainType — flatten to the DTO list shape.
    domainModifiers: (Object.entries(weapon.domainModifiers) as [DomainType, number][]).map(
      ([domain, damageMultiplier]) => ({ domain, damageMultiplier }),
    ),
  };
}

function mapArmour(armour: UnitComponentArmour) {
  return { resistance: mapDamageProfile(armour.resistance) };
}

function mapSlot(slot: UnitComponentSlot) {
  return {
    // The id is the upsert key: sending it tells the backend to update this exact slot row in place, which is
    // what keeps saved templates' fitted components attached. A freshly-added slot has no id and is inserted.
    // It also disambiguates a code rename, which the backend would otherwise read as remove-then-add.
    id: slot.id ?? null,
    code: slot.code,
    displayName: slot.displayName,
    isVisible: slot.isVisible,
    // C# property is UIXPosition; ASP.NET Core deserialises case-insensitively.
    uiXPosition: slot.uiXPosition,
    uiYPosition: slot.uiYPosition,
    rules: slot.rules.map((rule) => ({
      ruleType: rule.ruleType,
      operator: rule.operator,
      value: rule.value,
    })),
  };
}

/**
 * Bulk-upserts a single component via POST /unit-design/components/bulk. A component whose
 * code doesn't yet exist is inserted; one whose code already exists is updated in place.
 * Returns a summary of how many components were added vs updated.
 */
export async function bulkUpsertComponents(component: UnitComponent): Promise<BulkUpsertComponentsResult> {
  // Tags are referenced by code only (the backend resolves them against the existing catalog),
  // so the tags array is left empty — no inline tag definitions are sent.
  const payload = {
    tags: [],
    components: [mapComponent(component)],
    // Supersession links are authored relative to the predecessor (this component), so we expand
    // each link stored on the component into the flat { predecessorCode, successorCode } DTO shape.
    componentSupersessions: component.supersededBy.map((link) => ({
      predecessorCode: component.code,
      successorCode: link.successorCode,
      isRecommended: link.isRecommended,
    })),
  };

  const { data } = await http.post<BulkUpsertComponentsResult>("/unit-design/components/bulk", payload);
  return data;
}

/** Maps the editable component model onto the wire DTO shared by the bulk and single-component endpoints. */
function mapComponent(component: UnitComponent) {
  return {
    code: component.code,
    displayName: component.displayName,
    description: component.description,
    designerImagePath: component.designerImagePath,
    componentType: component.componentType,
    weight: component.weight,
    detection: component.detection,
    visibility: component.visibility,
    body: component.body != null ? mapBody(component.body) : null,
    weapon: component.weapon != null ? mapWeapon(component.weapon) : null,
    armour: component.armour != null ? mapArmour(component.armour) : null,
    tagCodes: component.tagCodes,
  };
}

/** Shape returned by the single-component create/update endpoints (mirrors UnitComponentView). */
export interface UnitComponentSaveResult {
  id: number;
  code: string;
  displayName: string;
  componentType: string;
  /** The component's slot ids after the save, ascending — the canonical slot order. */
  slotIds: number[];
  /** How many body slots the save deleted because they were removed from the payload. */
  removedSlotCount: number;
  /** Templates that lost a fitted component because the slot holding it was deleted. */
  unfittedTemplateNames: string[];
}

/**
 * Builds the single-component payload. The component's upgrade links ride alongside it rather than inside
 * it, since they are relationships between components rather than fields of one.
 */
function mapUpsertPayload(component: UnitComponent) {
  return {
    component: mapComponent(component),
    supersededBy: component.supersededBy.map((link) => ({
      successorCode: link.successorCode,
      isRecommended: link.isRecommended,
    })),
  };
}

/**
 * Creates a component via POST /unit-design/components. Fails with 409 if the code is already taken —
 * use `updateComponent` to edit an existing one.
 */
export async function createComponent(component: UnitComponent): Promise<UnitComponentSaveResult> {
  const { data } = await http.post<UnitComponentSaveResult>("/unit-design/components", mapUpsertPayload(component));
  return data;
}

/**
 * Updates an existing component via PUT /unit-design/components/{code}. Child collections are reconciled
 * rather than replaced, so slots keep their ids and saved templates keep their fitted components.
 */
export async function updateComponent(component: UnitComponent): Promise<UnitComponentSaveResult> {
  const { data } = await http.put<UnitComponentSaveResult>(
    `/unit-design/components/${encodeURIComponent(component.code)}`,
    mapUpsertPayload(component),
  );
  return data;
}

// --- GET one full component (edit flow) ---------------------------------------------------------
//
// The catalog's component DTO is lossy (no body stats / supersession, bodies excluded), so opening a
// component for edit fetches it in full from GET /unit-design/components/{code}. The wire shapes below
// mirror EditableComponentResult; mapEditableComponentToModel inverts the bulkUpsertComponents payload
// mapping back into the editable UnitComponent the dialog expects.

/** Server damage profile (raw Scale-1,000), PascalCase columns serialised camelCase. */
interface DamageProfileWire {
  crush: number;
  pierce: number;
  burn: number;
  slash: number;
  poison: number;
}

interface BodyWire {
  movementDomain: DomainType;
  // Raw Scale-1,000 fixed-point values.
  campaignMovementPoints: number;
  battlefieldMovementPoints: number;
  power: number;
  hitpoints: number;
  slots: SlotWire[];
}

interface WeaponWire {
  attack: DamageProfileWire;
  minimumRange: number;
  maximumRange: number;
  dualWield: boolean;
  domainModifiers: { domain: DomainType; damageMultiplier: number }[];
}

interface ArmourWire {
  resistance: DamageProfileWire;
}

interface SlotWire {
  id?: number;
  code: string;
  displayName: string | null;
  isVisible: boolean;
  // The C# property is UIXPosition; the MVC camelCase policy serialises it as "uixPosition", but we read
  // the alternate casing too in case the naming policy ever changes (same defensive pattern as elsewhere).
  uixPosition?: number;
  uiXPosition?: number;
  uiyPosition?: number;
  uiYPosition?: number;
  rules: { ruleType: UnitComponentSlotRule["ruleType"]; operator: UnitComponentSlotRule["operator"]; value: string }[];
}

interface ComponentWire {
  code: string;
  displayName: string;
  description: string | null;
  designerImagePath: string | null;
  componentType: ComponentType;
  weight: number;
  detection: number | null;
  visibility: number | null;
  body: BodyWire | null;
  weapon: WeaponWire | null;
  armour: ArmourWire | null;
  tagCodes: string[];
}

interface EditableComponentWire {
  id: number;
  component: ComponentWire;
  supersededBy: { predecessorCode: string; successorCode: string; isRecommended: boolean }[];
}

function mapDamageProfileFromWire(profile: DamageProfileWire): DamageProfile {
  return {
    crush: profile.crush,
    pierce: profile.pierce,
    burn: profile.burn,
    slash: profile.slash,
    poison: profile.poison,
  };
}

function mapBodyFromWire(body: BodyWire): UnitComponentBody {
  return {
    movementDomain: body.movementDomain,
    campaignMovementPoints: body.campaignMovementPoints,
    battlefieldMovementPoints: body.battlefieldMovementPoints,
    power: body.power,
    hitpoints: body.hitpoints,
    slots: body.slots.map(mapSlotFromWire),
  };
}

function mapWeaponFromWire(weapon: WeaponWire): UnitComponentWeapon {
  // The wire DTO is a flat list keyed by domain; rebuild the sparse record the model uses.
  const domainModifiers: WeaponDomainModifiers = {};
  for (const modifier of weapon.domainModifiers) {
    domainModifiers[modifier.domain] = modifier.damageMultiplier;
  }

  return {
    attack: mapDamageProfileFromWire(weapon.attack),
    minimumRange: weapon.minimumRange,
    maximumRange: weapon.maximumRange,
    dualWield: weapon.dualWield,
    domainModifiers,
  };
}

function mapArmourFromWire(armour: ArmourWire): UnitComponentArmour {
  return { resistance: mapDamageProfileFromWire(armour.resistance) };
}

function mapSlotFromWire(slot: SlotWire): UnitComponentSlot {
  return {
    id: slot.id,
    code: slot.code,
    displayName: slot.displayName,
    isVisible: slot.isVisible,
    uiXPosition: slot.uixPosition ?? slot.uiXPosition ?? 0,
    uiYPosition: slot.uiyPosition ?? slot.uiYPosition ?? 0,
    rules: slot.rules.map((rule) => ({
      ruleType: rule.ruleType,
      operator: rule.operator,
      value: rule.value,
    })),
  };
}

/** Maps the full GET /unit-design/components/{code} response back into the editable UnitComponent model. */
export function mapEditableComponentToModel(wire: EditableComponentWire): UnitComponent {
  const { component } = wire;

  // The editing component is always the predecessor, so keep only the successor side of each link.
  const supersededBy: ComponentSupersessionLink[] = wire.supersededBy.map((link) => ({
    successorCode: link.successorCode,
    isRecommended: link.isRecommended,
  }));

  return {
    id: wire.id,
    code: component.code,
    displayName: component.displayName,
    description: component.description,
    designerImagePath: component.designerImagePath,
    componentType: component.componentType,
    weight: component.weight,
    detection: component.detection,
    visibility: component.visibility,
    body: component.body != null ? mapBodyFromWire(component.body) : null,
    weapon: component.weapon != null ? mapWeaponFromWire(component.weapon) : null,
    armour: component.armour != null ? mapArmourFromWire(component.armour) : null,
    tagCodes: component.tagCodes,
    supersededBy,
  };
}

/** Fetch a single component in full by code and map it to the editable UnitComponent model. */
export async function getComponent(code: string): Promise<UnitComponent> {
  const { data } = await http.get<EditableComponentWire>(`/unit-design/components/${encodeURIComponent(code)}`);
  return mapEditableComponentToModel(data);
}
