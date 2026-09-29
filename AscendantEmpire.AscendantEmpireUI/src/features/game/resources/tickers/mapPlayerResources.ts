import type {
  PlayerResource,
  PlayerResourceCategory,
  PlayerResourceMetaCategory,
  ResourceCategoryTypeName,
  ResourceGradeName,
} from "@/features/game/api/playerResourcesApi";
import type { ResourceGrade } from "./ResourceConstants";
import type { ResourceCategoryData, ResourceCategoryType, ResourceTickerData, ResourceTickerGroupData } from "./ResourceTickerCollection";

/** The wire's ResourceGrade member names are the frontend union too, so this only guards against a name the frontend does not know. */
const GRADE_BY_WIRE_NAME: Record<ResourceGradeName, ResourceGrade> = {
  None: "None",
  Low: "Low",
  Medium: "Medium",
  High: "High",
};

/** The wire's ResourceCategoryType member names, mapped to the discriminant `ResourceTickerCollection` switches on. */
const CATEGORY_TYPE_BY_WIRE_NAME: Record<ResourceCategoryTypeName, ResourceCategoryType> = {
  GroupedResources: "grouped-resources",
  StandaloneResources: "standalone-resources",
};

function mapTicker(resource: PlayerResource): ResourceTickerData {
  return {
    id: resource.id,
    code: resource.code,
    name: resource.displayName,
    iconPath: resource.iconPath,
    amount: resource.amount,
    amountAddedPerTurn: resource.amountAddedPerTurn,
    description: resource.description ?? undefined,
    isLuxury: resource.isLuxury,
    isImported: resource.isImported,
    isExported: resource.isExported,
    hasStrategicStockpile: resource.hasStrategicStockpile,
    // The API sends null rather than zero when nothing is scheduled, and a set value is always positive, so there is nothing to clamp here.
    obsoleteIn: resource.obsoleteInRounds ?? undefined,
    demandNotMet: resource.demandNotMet,
    grade: GRADE_BY_WIRE_NAME[resource.resourceGrade] ?? "None",
  };
}

/**
 * Lays a grouped category's groups out across grid columns.
 *
 * The column is the group's 1-based position in the array, because the API has already sorted the groups — so re-ordering the catalogue re-lays the
 * panel out with no change here.
 */
function mapTickerGroups(category: PlayerResourceCategory): ResourceTickerGroupData[] {
  return category.groups.map((group, groupIndex) => ({
    id: group.id,
    code: group.code,
    label: group.displayName,
    column: groupIndex + 1,
    tickers: group.resources.map(mapTicker),
  }));
}

function mapCategory(category: PlayerResourceCategory): ResourceCategoryData {
  if (CATEGORY_TYPE_BY_WIRE_NAME[category.categoryType] === "grouped-resources") {
    return {
      id: category.id,
      code: category.code,
      type: "grouped-resources",
      label: category.displayName,
      tickerGroups: mapTickerGroups(category),
    };
  }

  return {
    id: category.id,
    code: category.code,
    type: "standalone-resources",
    label: category.displayName,
    resources: category.resources.map(mapTicker),
  };
}

/**
 * Everything one meta-category's panel shows, in the order the API returned it.
 *
 * A category whose own collection is empty still maps to a category, so the panel shows its header rather than silently dropping it; a meta-category
 * with no categories maps to an empty array, which renders as an empty panel.
 */
export function mapMetaCategoryToTickerCategories(metaCategory: PlayerResourceMetaCategory): ResourceCategoryData[] {
  return metaCategory.categories.map(mapCategory);
}
