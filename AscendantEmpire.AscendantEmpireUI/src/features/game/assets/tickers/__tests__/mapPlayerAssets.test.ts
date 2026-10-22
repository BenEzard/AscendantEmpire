import type { PlayerAsset, PlayerAssetCategory, PlayerAssetGroup, PlayerAssetMetaCategory } from "@/features/game/api/playerAssetsApi";
import { mapMetaCategoryToTickerCategories } from "../mapPlayerAssets";

function aAsset(overrides: Partial<PlayerAsset> = {}): PlayerAsset {
  return {
    id: 1,
    code: "Wood.Logs.OakLog",
    displayName: "Oak Log",
    description: null,
    iconPath: "/resources/wood/oak-log_40px.png",
    isGradedAsset: false,
    assetGrade: "None",
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

function aGroup(overrides: Partial<PlayerAssetGroup> = {}): PlayerAssetGroup {
  return {
    id: 100,
    code: "Wood.Logs",
    displayName: "Logs",
    subjectPlayerId: null,
    sortOrder: 1,
    assets: [aAsset()],
    ...overrides,
  };
}

function aGroupedCategory(overrides: Partial<PlayerAssetCategory> = {}): PlayerAssetCategory {
  return {
    id: 10,
    code: "Wood",
    displayName: "Wood",
    categoryType: "GroupedAssets",
    sortOrder: 1,
    groups: [
      aGroup(),
      aGroup({ id: 101, code: "Wood.Planks", displayName: "Planks", sortOrder: 2, assets: [aAsset({ id: 2, code: "Wood.Planks.OakPlank" })] }),
    ],
    assets: [],
    ...overrides,
  };
}

function aTree(categories: PlayerAssetCategory[]): PlayerAssetMetaCategory {
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
 * RS-12: the panel is built from GET /player/assets, and this module is the only place the wire vocabulary becomes the view model's — so the
 * discriminant, the grade names, the obsoleting units and the derived grid column are all pinned here.
 */
describe("mapMetaCategoryToTickerCategories", () => {
  it("maps the wire's GroupedAssets to the grouped discriminant and keeps its groups", () => {
    const [category] = mapMetaCategoryToTickerCategories(aTree([aGroupedCategory()]));

    expect(category.type).toBe("grouped-assets");
    expect(category.type === "grouped-assets" && category.tickerGroups.map((group) => group.label)).toEqual(["Logs", "Planks"]);
  });

  it("maps the wire's StandaloneAssets to the standalone discriminant and keeps its assets", () => {
    const standalone = aGroupedCategory({ code: "Other", displayName: "Other", categoryType: "StandaloneAssets", groups: [], assets: [aAsset()] });

    const [category] = mapMetaCategoryToTickerCategories(aTree([standalone]));

    expect(category.type).toBe("standalone-assets");
    expect(category.type === "standalone-assets" && category.assets.map((asset) => asset.name)).toEqual(["Oak Log"]);
  });

  it("takes a group's grid column from its 1-based position in the order the API returned", () => {
    const [category] = mapMetaCategoryToTickerCategories(aTree([aGroupedCategory()]));

    expect(category.type === "grouped-assets" && category.tickerGroups.map((group) => group.column)).toEqual([1, 2]);
  });

  it("carries a known grade through and falls back to None for one the frontend does not know", () => {
    const category = aGroupedCategory({
      categoryType: "StandaloneAssets",
      groups: [],
      assets: [
        aAsset({ id: 1, code: "a", assetGrade: "High" }),
        // A grade added on the API before the frontend union catches up: the ticker must render, just without a shield.
        aAsset({ id: 2, code: "b", assetGrade: "Legendary" as never }),
      ],
    });

    const [mapped] = mapMetaCategoryToTickerCategories(aTree([category]));

    expect(mapped.type === "standalone-assets" && mapped.assets.map((asset) => asset.grade)).toEqual(["High", "None"]);
  });

  it("drops a null obsoleteInRounds rather than reading it as obsoleting this round, and carries a real countdown", () => {
    const category = aGroupedCategory({
      categoryType: "StandaloneAssets",
      groups: [],
      assets: [aAsset({ id: 1, code: "a", obsoleteInRounds: null }), aAsset({ id: 2, code: "b", obsoleteInRounds: 12 })],
    });

    const [mapped] = mapMetaCategoryToTickerCategories(aTree([category]));

    expect(mapped.type === "standalone-assets" && mapped.assets.map((asset) => asset.obsoleteIn)).toEqual([undefined, 12]);
  });

  it("renames displayName to name and carries amountAddedPerTurn under the wire's spelling", () => {
    const category = aGroupedCategory({
      categoryType: "StandaloneAssets",
      groups: [],
      assets: [aAsset({ displayName: "Oak Log", amountAddedPerTurn: 40 })],
    });

    const [mapped] = mapMetaCategoryToTickerCategories(aTree([category]));

    expect(mapped.type === "standalone-assets" && mapped.assets[0]).toMatchObject({ name: "Oak Log", amountAddedPerTurn: 40 });
  });

  it("carries a projected group's subject player through, since the people name is the label and the id is the identity", () => {
    // What the Enemies panel is: one category, one column per opponent, each labelled with that opponent's people name.
    const enemies = aGroupedCategory({
      code: "Society.ForeignPopulation",
      displayName: "Enemies",
      groups: [
        aGroup({ id: 200, code: "Society.ForeignPopulation.Subjugated.P7", displayName: "Geoffs", subjectPlayerId: 7 }),
        aGroup({ id: 200, code: "Society.ForeignPopulation.Subjugated.P9", displayName: "Romans", subjectPlayerId: 9 }),
      ],
    });

    const [mapped] = mapMetaCategoryToTickerCategories(aTree([enemies]));

    expect(mapped.label).toBe("Enemies");
    expect(mapped.type === "grouped-assets" && mapped.tickerGroups.map((group) => [group.label, group.subjectPlayerId, group.column])).toEqual([
      ["Geoffs", 7, 1],
      ["Romans", 9, 2],
    ]);
  });

  it("maps a meta-category with nothing unlocked under it to an empty panel", () => {
    expect(mapMetaCategoryToTickerCategories(aTree([]))).toEqual([]);
  });

  it("keeps a grouped category whose group holds no assets, so the panel shows its header rather than dropping it", () => {
    const category = aGroupedCategory({ groups: [aGroup({ assets: [] })] });

    const [mapped] = mapMetaCategoryToTickerCategories(aTree([category]));

    expect(mapped.type === "grouped-assets" && mapped.tickerGroups).toHaveLength(1);
    expect(mapped.type === "grouped-assets" && mapped.tickerGroups[0].tickers).toEqual([]);
  });
});
