import { describe, expect, it } from "vitest";
import { ExtractionMethod, type MapResourceCategory, type MapResourceType } from "@/types/map/MapResource";
import {
  createHabitat,
  createLayer,
  createMapResourceType,
  fromPercentInput,
  fromWholeInput,
  kindOf,
  renumberLayers,
  toDraft,
  toPayload,
  toPercentInput,
  withExtractionMethod,
  withKind,
} from "../mapResourceDraft";

const FOREST = 8;
const HILLY = 4;

/** A renewable type with a yield and a spreading Forest habitat. */
function renewableType(): MapResourceType {
  const type = createMapResourceType("food", 10);

  return {
    ...type,
    code: "berry",
    yields: [{ resourceId: 1, amountPerUnit: 1, isEnabledByDefault: true }],
    habitats: [{ ...createHabitat("renewable", FOREST), spreadLikelihood: 0.1, spreadLimitMin: 1, spreadLimitMax: 3 }],
  };
}

describe("kindOf", () => {
  it("reads each kind from its flags", () => {
    expect(kindOf({ isRenewable: false, isStratified: false, isDistribution: false })).toBe("fixed");
    expect(kindOf({ isRenewable: true, isStratified: false, isDistribution: false })).toBe("renewable");
    expect(kindOf({ isRenewable: true, isStratified: false, isDistribution: true })).toBe("distribution");
    expect(kindOf({ isRenewable: false, isStratified: true, isDistribution: false })).toBe("stratified");
  });
});

describe("withKind", () => {
  it("starts a new type as renewable, with the values a renewable type needs", () => {
    const type = createMapResourceType("food", 10);

    expect(kindOf(type)).toBe("renewable");
    expect(type.survivalRounds).not.toBeNull();
    expect(type.reseedLikelihood).not.toBeNull();
    expect(type.estimationFuzzyFactorMin).not.toBeNull();
  });

  it("clears growth, survival and spread when a renewable type becomes fixed, keeping its yields and initial quantities", () => {
    const fixed = withKind(renewableType(), "fixed");

    expect(fixed.isRenewable).toBe(false);
    expect(fixed.survivalRounds).toBeNull();
    expect(fixed.reseedLikelihood).toBeNull();
    expect(fixed.yields).toHaveLength(1);
    expect(fixed.habitats[0]).toMatchObject({ maximumQuantity: null, growthPercentage: null, spreadLikelihood: null, spreadLimitMin: null });
    expect(fixed.habitats[0].initialQuantityMin).not.toBeNull();
  });

  it("makes a Distribution renewable, drops its yields and spread, and gives its habitats no initial quantity", () => {
    const distribution = withKind(renewableType(), "distribution");

    expect(distribution).toMatchObject({ isRenewable: true, isDistribution: true, isStratified: false, yields: [] });
    expect(distribution.habitats[0]).toMatchObject({ initialQuantityMin: null, initialQuantityMax: null, spreadLikelihood: null });
    expect(distribution.habitats[0].maximumQuantity).not.toBeNull();
  });

  it("gives a stratified type no type-level fuzzy range, no survival values, and habitats with no values", () => {
    const stratified = withKind(renewableType(), "stratified");

    expect(stratified).toMatchObject({ isRenewable: false, isStratified: true, estimationFuzzyFactorMin: null, survivalRounds: null, yields: [] });
    expect(stratified.habitats[0]).toMatchObject({ initialQuantityMin: null, maximumQuantity: null, growthPercentage: null });
  });

  it("keeps a habitat's Geography and Biome whatever the kind", () => {
    const type = renewableType();
    type.habitats[0].geographyType = HILLY;

    expect(withKind(type, "stratified").habitats[0]).toMatchObject({ biomeType: FOREST, geographyType: HILLY });
  });
});

describe("withExtractionMethod", () => {
  it("sets a CaptureLikelihood only for Capture, and clears it for anything else", () => {
    const captured = withExtractionMethod(renewableType(), ExtractionMethod.Capture);
    const hunted = withExtractionMethod(captured, ExtractionMethod.Hunt);

    expect(captured.captureLikelihood).not.toBeNull();
    expect(hunted.captureLikelihood).toBeNull();
  });
});

describe("layers", () => {
  it("gives the first layer no gates and every deeper layer both", () => {
    expect(createLayer(1, 5)).toMatchObject({ discoveryLikelihood: null, requiredMineLevel: null });
    expect(createLayer(2, 5).discoveryLikelihood).not.toBeNull();
    expect(createLayer(2, 5).requiredMineLevel).not.toBeNull();
  });

  it("renumbers from 1 after a layer is removed, moving the gates with the depth", () => {
    const renumbered = renumberLayers([createLayer(2, 5), createLayer(3, 6)]);

    expect(renumbered.map((l) => l.layerNumber)).toEqual([1, 2]);
    expect(renumbered[0]).toMatchObject({ resourceId: 5, discoveryLikelihood: null, requiredMineLevel: null });
    expect(renumbered[1].discoveryLikelihood).not.toBeNull();
  });
});

describe("toDraft and toPayload", () => {
  it("flattens the catalogue and sends it back without ids", () => {
    const berry: MapResourceType = { ...renewableType(), id: 7 };
    const catalogue: MapResourceCategory[] = [{ id: 3, code: "food", displayName: "Food", sortOrder: 10, types: [berry] }];

    const draft = toDraft(catalogue);
    const payload = toPayload(draft);

    expect(draft.categories).toEqual([{ id: 3, code: "food", displayName: "Food", sortOrder: 10 }]);
    expect(draft.types).toHaveLength(1);
    expect(payload.categories[0]).not.toHaveProperty("id");
    expect(payload.types[0]).not.toHaveProperty("id");
    expect(payload.types[0].code).toBe("berry");
  });

  it("treats a missing catalogue as empty", () => {
    expect(toDraft(undefined)).toEqual({ categories: [], types: [] });
  });
});

describe("input conversion", () => {
  it("shows fractions as percents and reads them back", () => {
    expect(toPercentInput(0.07)).toBe(7);
    expect(toPercentInput(null)).toBe("");
    expect(fromPercentInput("12.5")).toBe(0.125);
    expect(fromPercentInput(" ")).toBeNull();
  });

  it("reads whole numbers, dropping any fraction", () => {
    expect(fromWholeInput("42")).toBe(42);
    expect(fromWholeInput("4.9")).toBe(4);
    expect(fromWholeInput("")).toBeNull();
  });
});
