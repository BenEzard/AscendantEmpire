/**
 * Pure helpers behind the MapResource Type Manager: turning the loaded catalogue into an editable draft and back, and keeping a type's values
 * consistent with its kind as the designer edits it.
 *
 * A MapResourceType's kind is set by three server-side flags (IsRenewable, IsStratified, IsDistribution), and the server's validator refuses any
 * value that does not apply to the kind rather than ignoring it. So instead of letting the designer tick flags into an invalid combination, the
 * manager offers four kinds, and switching kind here clears whatever the new kind does not use and fills in defaults for what it now needs.
 */
import { ExtractionMethod, type MapResourceCategory, type MapResourceHabitat, type MapResourceType, type MapResourceTypeLayer } from "@/types/map/MapResource";
import type { MapResourceCataloguePayload, MapResourceTypePayload } from "../api/mapResourcesApi";

/** The four valid combinations of the three kind flags. */
export type MapResourceKind = "fixed" | "renewable" | "distribution" | "stratified";

export const MAP_RESOURCE_KINDS: { kind: MapResourceKind; label: string; description: string }[] = [
  { kind: "fixed", label: "Fixed", description: "A set amount that never grows, giving its yields." },
  { kind: "renewable", label: "Renewable", description: "Grows every Round and may spread, giving its yields." },
  { kind: "distribution", label: "Distribution", description: "Grows, never spreads, and holds several Resources from a scenario." },
  { kind: "stratified", label: "Stratified", description: "Never grows; held in layers reached in order." },
];

export const EXTRACTION_METHODS: { value: ExtractionMethod; label: string }[] = [
  { value: ExtractionMethod.Gather, label: "Gather" },
  { value: ExtractionMethod.Hunt, label: "Hunt" },
  { value: ExtractionMethod.Fell, label: "Fell" },
  { value: ExtractionMethod.Mine, label: "Mine" },
  { value: ExtractionMethod.Capture, label: "Capture" },
];

// Defaults filled in when a value becomes required. Plain starting points for the designer to change, not game rules.
const DEFAULT_SURVIVAL_ROUNDS = 5;
const DEFAULT_RESEED_LIKELIHOOD = 0.1;
const DEFAULT_CAPTURE_LIKELIHOOD = 0.5;
const DEFAULT_FUZZY_MIN = 0.1;
const DEFAULT_FUZZY_MAX = 0.3;

/** The editable copy of the catalogue: categories without their types, and every type in one flat list naming its category by code. */
export type MapResourceDraft = {
  categories: { id?: number; code: string; displayName: string; sortOrder: number }[];
  types: MapResourceType[];
};

/** Flattens the loaded catalogue into a draft. */
export function toDraft(categories: MapResourceCategory[] | undefined): MapResourceDraft {
  const list = categories ?? [];

  return {
    categories: list.map(({ id, code, displayName, sortOrder }) => ({ id, code, displayName, sortOrder })),
    types: list.flatMap((category) => category.types),
  };
}

/** The draft as PUT /map-resources/catalogue takes it. Ids are left out: the server matches on code. */
export function toPayload(draft: MapResourceDraft): MapResourceCataloguePayload {
  return {
    categories: draft.categories.map(({ code, displayName, sortOrder }) => ({ code, displayName, sortOrder })),
    types: draft.types.map(toTypePayload),
  };
}

/** A copy of the type without the two values the server owns, so the body matches the add-or-update contract exactly. */
function toTypePayload(type: MapResourceType): MapResourceTypePayload {
  // Widened to Partial so sortOrder, which is required on the loaded type, can be deleted without a cast of its own.
  const copy: Partial<MapResourceType> = { ...type };
  delete copy.id;
  delete copy.sortOrder;

  return copy as MapResourceTypePayload;
}

/** Which of the four kinds the type's flags describe. */
export function kindOf(type: Pick<MapResourceType, "isRenewable" | "isStratified" | "isDistribution">): MapResourceKind {
  if (type.isStratified) return "stratified";
  if (type.isDistribution) return "distribution";
  if (type.isRenewable) return "renewable";
  return "fixed";
}

/** Whether instances of this kind hold a single quantity (and so start from an initial quantity range and give yields). */
export function holdsSingleQuantity(kind: MapResourceKind): boolean {
  return kind === "fixed" || kind === "renewable";
}

/** Whether instances of this kind grow each Round (and so have a ceiling, a growth percentage and survival values). */
export function grows(kind: MapResourceKind): boolean {
  return kind === "renewable" || kind === "distribution";
}

/** Whether instances of this kind can spread to neighbouring tiles. */
export function canSpread(kind: MapResourceKind): boolean {
  return kind === "renewable";
}

/** A habitat with only the values `kind` uses, defaulting any it newly needs and clearing the rest. */
export function habitatForKind(habitat: MapResourceHabitat, kind: MapResourceKind): MapResourceHabitat {
  const single = holdsSingleQuantity(kind);
  const growing = grows(kind);
  const spreading = canSpread(kind);

  return {
    ...habitat,
    initialQuantityMin: single ? (habitat.initialQuantityMin ?? 0) : null,
    initialQuantityMax: single ? (habitat.initialQuantityMax ?? 0) : null,
    maximumQuantity: growing ? (habitat.maximumQuantity ?? Math.max(habitat.initialQuantityMax ?? 0, 1)) : null,
    growthPercentage: growing ? (habitat.growthPercentage ?? 0.1) : null,
    // Spread stays optional for a renewable type (all three or none), so existing values are kept and nothing is invented.
    spreadLikelihood: spreading ? habitat.spreadLikelihood : null,
    spreadLimitMin: spreading ? habitat.spreadLimitMin : null,
    spreadLimitMax: spreading ? habitat.spreadLimitMax : null,
  };
}

/** The type switched to `kind`: flags set, values and children the kind does not use cleared, and anything it now needs given a default. */
export function withKind(type: MapResourceType, kind: MapResourceKind): MapResourceType {
  const growing = grows(kind);
  const stratified = kind === "stratified";

  return {
    ...type,
    isRenewable: growing,
    isDistribution: kind === "distribution",
    isStratified: stratified,
    // A stratified type judges each layer separately, so the type-level range goes; every other kind needs one.
    estimationFuzzyFactorMin: stratified ? null : (type.estimationFuzzyFactorMin ?? DEFAULT_FUZZY_MIN),
    estimationFuzzyFactorMax: stratified ? null : (type.estimationFuzzyFactorMax ?? DEFAULT_FUZZY_MAX),
    survivalRounds: growing ? (type.survivalRounds ?? DEFAULT_SURVIVAL_ROUNDS) : null,
    reseedLikelihood: growing ? (type.reseedLikelihood ?? DEFAULT_RESEED_LIKELIHOOD) : null,
    // Each kind holds its Resources in exactly one of these three ways.
    yields: holdsSingleQuantity(kind) ? type.yields : [],
    distributionScenarios: kind === "distribution" ? type.distributionScenarios : [],
    layers: stratified ? type.layers : [],
    habitats: type.habitats.map((habitat) => habitatForKind(habitat, kind)),
  };
}

/** The type with a new extraction method, keeping CaptureLikelihood set exactly when the method is Capture. */
export function withExtractionMethod(type: MapResourceType, method: ExtractionMethod): MapResourceType {
  const isCapture = method === ExtractionMethod.Capture;

  return {
    ...type,
    extractionMethod: method,
    captureLikelihood: isCapture ? (type.captureLikelihood ?? DEFAULT_CAPTURE_LIKELIHOOD) : null,
  };
}

/** A new renewable type in the given category, with no code yet (the manager derives one from the name). */
export function createMapResourceType(categoryCode: string, sortOrder: number): MapResourceType {
  return withKind(
    {
      code: "",
      displayName: "New MapResource",
      mapResourceCategoryCode: categoryCode,
      sortOrder,
      iconPath: null,
      isRenewable: false,
      isStratified: false,
      isDistribution: false,
      extractionMethod: ExtractionMethod.Gather,
      captureLikelihood: null,
      requiresResearchToExtract: false,
      isDiscoverableByDefault: true,
      discoveryLikelihood: 1,
      estimationFuzzyFactorMin: null,
      estimationFuzzyFactorMax: null,
      survivalRounds: null,
      reseedLikelihood: null,
      yields: [],
      habitats: [],
      distributionScenarios: [],
      layers: [],
    },
    "renewable",
  );
}

/** A new habitat for `biomeType` (every Geography), with the values the kind needs. */
export function createHabitat(kind: MapResourceKind, biomeType: number): MapResourceHabitat {
  return habitatForKind(
    {
      biomeType,
      geographyType: null,
      initialQuantityMin: null,
      initialQuantityMax: null,
      maximumQuantity: null,
      growthPercentage: null,
      spreadLikelihood: null,
      spreadLimitMin: null,
      spreadLimitMax: null,
    },
    kind,
  );
}

/** A new layer at `layerNumber`. Layer 1 has no gates of its own; deeper layers need a discovery likelihood and a mine level. */
export function createLayer(layerNumber: number, resourceId: number): MapResourceTypeLayer {
  return withLayerNumber(
    {
      layerNumber,
      resourceId,
      emptyLikelihood: 0,
      quantityMin: 0,
      quantityMax: 0,
      discoveryLikelihood: null,
      requiredMineLevel: null,
      estimationFuzzyFactorMin: DEFAULT_FUZZY_MIN,
      estimationFuzzyFactorMax: DEFAULT_FUZZY_MAX,
    },
    layerNumber,
  );
}

/** Numbers the layers 1, 2, 3… in their current order, fixing each layer's gates to suit its new depth. Used after a layer is removed. */
export function renumberLayers(layers: MapResourceTypeLayer[]): MapResourceTypeLayer[] {
  return layers.map((layer, index) => withLayerNumber(layer, index + 1));
}

function withLayerNumber(layer: MapResourceTypeLayer, layerNumber: number): MapResourceTypeLayer {
  const isFirst = layerNumber === 1;

  return {
    ...layer,
    layerNumber,
    discoveryLikelihood: isFirst ? null : (layer.discoveryLikelihood ?? 0.5),
    requiredMineLevel: isFirst ? null : (layer.requiredMineLevel ?? layerNumber - 1),
  };
}

/** The next free number: one above the highest in use, or 1. */
export function nextNumber(numbers: number[]): number {
  return numbers.length === 0 ? 1 : Math.max(...numbers) + 1;
}

// ---------------------------------------------------------------------------
// Input conversion
// ---------------------------------------------------------------------------

/** A stored fraction (0.25) as the percent the manager shows (25), to one decimal place, or "" for none. */
export function toPercentInput(fraction: number | null | undefined): string | number {
  if (fraction == null) {
    return "";
  }

  // Rounded to tenths of a percent (the server keeps three decimal places of the fraction) so float noise never reaches the input.
  return Math.round(fraction * 1000) / 10;
}

/** A typed percent as the stored fraction, to three decimal places, or null when empty. */
export function fromPercentInput(raw: string): number | null {
  if (raw.trim() === "") {
    return null;
  }

  return Math.round(Number(raw) * 10) / 1000;
}

/** A typed whole number, or null when empty. Fractions are dropped: the fields this feeds are whole numbers on the server. */
export function fromWholeInput(raw: string): number | null {
  if (raw.trim() === "") {
    return null;
  }

  return Math.trunc(Number(raw));
}
