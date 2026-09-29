import type { PlayerResource, PlayerResourceCategory, PlayerResourceMetaCategory } from "@/features/game/api/playerResourcesApi";
import { mapMetaCategoryToTickerCategories } from "../mapPlayerResources";

function aResource(overrides: Partial<PlayerResource> = {}): PlayerResource {
  return {
    id: 1,
    code: "Wood.Logs.OakLog",
    displayName: "Oak Log",
    description: null,
    iconPath: "/resources/wood/oak-log_40px.png",
    isGradedResource: false,
    resourceGrade: "None",
    sortOrder: 1,
    neverLuxury: false,
    obsoleteInRounds: null,
    amount: 0,
    amountAddedPerTurn: 0,
    hasStrategicStockpile: false,
    isImported: false,
    isExported: false,
    demandNotMet: false,
    isLuxury: false,
    ...overrides,
  };
}

function aGroupedCategory(overrides: Partial<PlayerResourceCategory> = {}): PlayerResourceCategory {
  return {
    id: 10,
    code: "Wood",
    displayName: "Wood",
    categoryType: "GroupedResources",
    sortOrder: 1,
    groups: [
      { id: 100, code: "Wood.Logs", displayName: "Logs", sortOrder: 1, resources: [aResource()] },
      { id: 101, code: "Wood.Planks", displayName: "Planks", sortOrder: 2, resources: [aResource({ id: 2, code: "Wood.Planks.OakPlank" })] },
    ],
    resources: [],
    ...overrides,
  };
}

function aTree(categories: PlayerResourceCategory[]): PlayerResourceMetaCategory {
  return {
    id: 1,
    code: "ConstructionMaterials",
    displayName: "Construction Materials",
    iconPath: "/resources/wood/oak-log_40px.png",
    sortOrder: 1,
    categories,
  };
}

/**
 * RS-12: the panel is built from GET /player/resources, and this module is the only place the wire vocabulary becomes the view model's — so the
 * discriminant, the grade names, the obsoleting units and the derived grid column are all pinned here.
 */
describe("mapMetaCategoryToTickerCategories", () => {
  it("maps the wire's GroupedResources to the grouped discriminant and keeps its groups", () => {
    const [category] = mapMetaCategoryToTickerCategories(aTree([aGroupedCategory()]));

    expect(category.type).toBe("grouped-resources");
    expect(category.type === "grouped-resources" && category.tickerGroups.map((group) => group.label)).toEqual(["Logs", "Planks"]);
  });

  it("maps the wire's StandaloneResources to the standalone discriminant and keeps its resources", () => {
    const standalone = aGroupedCategory({ code: "Other", displayName: "Other", categoryType: "StandaloneResources", groups: [], resources: [aResource()] });

    const [category] = mapMetaCategoryToTickerCategories(aTree([standalone]));

    expect(category.type).toBe("standalone-resources");
    expect(category.type === "standalone-resources" && category.resources.map((resource) => resource.name)).toEqual(["Oak Log"]);
  });

  it("takes a group's grid column from its 1-based position in the order the API returned", () => {
    const [category] = mapMetaCategoryToTickerCategories(aTree([aGroupedCategory()]));

    expect(category.type === "grouped-resources" && category.tickerGroups.map((group) => group.column)).toEqual([1, 2]);
  });

  it("carries a known grade through and falls back to None for one the frontend does not know", () => {
    const category = aGroupedCategory({
      categoryType: "StandaloneResources",
      groups: [],
      resources: [
        aResource({ id: 1, code: "a", resourceGrade: "High" }),
        // A grade added on the API before the frontend union catches up: the ticker must render, just without a shield.
        aResource({ id: 2, code: "b", resourceGrade: "Legendary" as never }),
      ],
    });

    const [mapped] = mapMetaCategoryToTickerCategories(aTree([category]));

    expect(mapped.type === "standalone-resources" && mapped.resources.map((resource) => resource.grade)).toEqual(["High", "None"]);
  });

  it("drops a null obsoleteInRounds rather than reading it as obsoleting this round, and carries a real countdown", () => {
    const category = aGroupedCategory({
      categoryType: "StandaloneResources",
      groups: [],
      resources: [aResource({ id: 1, code: "a", obsoleteInRounds: null }), aResource({ id: 2, code: "b", obsoleteInRounds: 12 })],
    });

    const [mapped] = mapMetaCategoryToTickerCategories(aTree([category]));

    expect(mapped.type === "standalone-resources" && mapped.resources.map((resource) => resource.obsoleteIn)).toEqual([undefined, 12]);
  });

  it("renames displayName to name and carries amountAddedPerTurn under the wire's spelling", () => {
    const category = aGroupedCategory({
      categoryType: "StandaloneResources",
      groups: [],
      resources: [aResource({ displayName: "Oak Log", amountAddedPerTurn: 40 })],
    });

    const [mapped] = mapMetaCategoryToTickerCategories(aTree([category]));

    expect(mapped.type === "standalone-resources" && mapped.resources[0]).toMatchObject({ name: "Oak Log", amountAddedPerTurn: 40 });
  });

  it("maps a meta-category with nothing unlocked under it to an empty panel", () => {
    expect(mapMetaCategoryToTickerCategories(aTree([]))).toEqual([]);
  });

  it("keeps a grouped category whose group holds no resources, so the panel shows its header rather than dropping it", () => {
    const category = aGroupedCategory({ groups: [{ id: 100, code: "Wood.Logs", displayName: "Logs", sortOrder: 1, resources: [] }] });

    const [mapped] = mapMetaCategoryToTickerCategories(aTree([category]));

    expect(mapped.type === "grouped-resources" && mapped.tickerGroups).toHaveLength(1);
    expect(mapped.type === "grouped-resources" && mapped.tickerGroups[0].tickers).toEqual([]);
  });
});
