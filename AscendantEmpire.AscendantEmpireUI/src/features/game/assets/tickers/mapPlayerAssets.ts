import type {
  PlayerAsset,
  PlayerAssetCategory,
  PlayerAssetMetaCategory,
  AssetCategoryTypeName,
  AssetGradeName,
} from "@/features/game/api/playerAssetsApi";
import type { AssetGrade } from "./AssetConstants";
import type { AssetCategoryData, AssetCategoryType, AssetTickerData, AssetTickerGroupData } from "./AssetTickerCollection";

/** The wire's AssetGrade member names are the frontend union too, so this only guards against a name the frontend does not know. */
const GRADE_BY_WIRE_NAME: Record<AssetGradeName, AssetGrade> = {
  None: "None",
  Low: "Low",
  Medium: "Medium",
  High: "High",
};

/** The wire's AssetCategoryType member names, mapped to the discriminant `AssetTickerCollection` switches on. */
const CATEGORY_TYPE_BY_WIRE_NAME: Record<AssetCategoryTypeName, AssetCategoryType> = {
  GroupedAssets: "grouped-assets",
  StandaloneAssets: "standalone-assets",
};

function mapTicker(asset: PlayerAsset): AssetTickerData {
  return {
    id: asset.id,
    code: asset.code,
    name: asset.displayName,
    iconPath: asset.iconPath,
    amount: asset.amount,
    amountAddedPerTurn: asset.amountAddedPerTurn,
    description: asset.description ?? undefined,
    isLuxury: asset.isLuxury,
    isImported: asset.isImported,
    isExported: asset.isExported,
    hasStrategicStockpile: asset.hasStrategicStockpile,
    // The API sends null rather than zero when nothing is scheduled, and a set value is always positive, so there is nothing to clamp here.
    obsoleteIn: asset.obsoleteInRounds ?? undefined,
    demandNotMet: asset.demandNotMet,
    grade: GRADE_BY_WIRE_NAME[asset.assetGrade] ?? "None",
  };
}

/**
 * Lays a grouped category's groups out across grid columns.
 *
 * The column is the group's 1-based position in the array, because the API has already sorted the groups — so re-ordering the catalogue re-lays the
 * panel out with no change here.
 */
function mapTickerGroups(category: PlayerAssetCategory): AssetTickerGroupData[] {
  return category.groups.map((group, groupIndex) => ({
    id: group.id,
    code: group.code,
    label: group.displayName,
    column: groupIndex + 1,
    tickers: group.assets.map(mapTicker),
  }));
}

function mapCategory(category: PlayerAssetCategory): AssetCategoryData {
  if (CATEGORY_TYPE_BY_WIRE_NAME[category.categoryType] === "grouped-assets") {
    return {
      id: category.id,
      code: category.code,
      type: "grouped-assets",
      label: category.displayName,
      tickerGroups: mapTickerGroups(category),
    };
  }

  return {
    id: category.id,
    code: category.code,
    type: "standalone-assets",
    label: category.displayName,
    assets: category.assets.map(mapTicker),
  };
}

/**
 * Everything one meta-category's panel shows, in the order the API returned it.
 *
 * A category whose own collection is empty still maps to a category, so the panel shows its header rather than silently dropping it; a meta-category
 * with no categories maps to an empty array, which renders as an empty panel.
 */
export function mapMetaCategoryToTickerCategories(metaCategory: PlayerAssetMetaCategory): AssetCategoryData[] {
  return metaCategory.categories.map(mapCategory);
}
