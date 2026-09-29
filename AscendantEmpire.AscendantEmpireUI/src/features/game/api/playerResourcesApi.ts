import { http } from "@/api/http";

/** The API's `ResourceGrade` member names. */
export type ResourceGradeName = "None" | "Low" | "Medium" | "High";

/** The API's `ResourceCategoryType` member names. */
export type ResourceCategoryTypeName = "StandaloneResources" | "GroupedResources";

/**
 * One Resource the Player can see, as the resource tree reports it.
 *
 * A graded Resource spells its grade out as the last segment of its dotted `code`, because every grade of it shares one `displayName`.
 */
export type PlayerResource = {
  id: number;
  /** Hierarchical and dotted, e.g. "Weapons.MeleeWeapons.BoneKnife.Low". Unique, and what the panel keys its tickers by. */
  code: string;
  displayName: string;
  description: string | null;
  /** Root-relative URL of the Resource's icon, rendered straight into an image source. Empty when the catalogue has none. */
  iconPath: string;
  isGradedResource: boolean;
  resourceGrade: ResourceGradeName;
  sortOrder: number;
  neverLuxury: boolean;
  /**
   * Rounds until this Resource stops being available, or null when nothing is scheduled.
   *
   * Null rather than zero, since a client would read zero as "obsoleting this Round". Always positive when set.
   */
  obsoleteInRounds: number | null;
  amount: number;
  amountAddedPerTurn: number;
  hasStrategicStockpile: boolean;
  isImported: boolean;
  isExported: boolean;
  demandNotMet: boolean;
  isLuxury: boolean;
};

/** A row of Resources inside a grouped category — the panel lays one group out per grid column. */
export type PlayerResourceGroup = {
  id: number;
  code: string;
  displayName: string;
  /** Null where the group carries no explicit order, in which case the API has already sorted it after every numbered group and then by name. */
  sortOrder: number | null;
  resources: PlayerResource[];
};

/**
 * One category under a meta-category.
 *
 * Exactly one of `groups` and `resources` is populated, chosen by `categoryType`. The other is empty rather than null, so either arm can be
 * rendered without a null check.
 */
export type PlayerResourceCategory = {
  id: number;
  code: string;
  displayName: string;
  categoryType: ResourceCategoryTypeName;
  sortOrder: number;
  groups: PlayerResourceGroup[];
  resources: PlayerResource[];
};

/**
 * One unlocked meta-category: a button in the Resources toolbar group, and the panel behind it.
 *
 * `categories` may be empty — an unlocked meta-category with nothing available under it still shows its toolbar icon, over an empty panel.
 */
export type PlayerResourceMetaCategory = {
  id: number;
  code: string;
  displayName: string;
  /** Root-relative URL of the icon shown on this meta-category's toolbar button. Empty when the catalogue has none. */
  iconPath: string;
  sortOrder: number;
  categories: PlayerResourceCategory[];
};

type PlayerResourceWireDto = Partial<PlayerResource>;

type PlayerResourceGroupWireDto = Partial<Omit<PlayerResourceGroup, "resources"> & { resources: PlayerResourceWireDto[] }>;

type PlayerResourceCategoryWireDto = Partial<
  Omit<PlayerResourceCategory, "groups" | "resources"> & { groups: PlayerResourceGroupWireDto[]; resources: PlayerResourceWireDto[] }
>;

type PlayerResourceMetaCategoryWireDto = Partial<Omit<PlayerResourceMetaCategory, "categories"> & { categories: PlayerResourceCategoryWireDto[] }>;

type PlayerResourceTreeWireDto = Partial<{ metaCategories: PlayerResourceMetaCategoryWireDto[] }>;

/** Reads one Resource off the wire, filling in anything the server left out. Shared by the grouped and standalone arms of a category. */
function mapResource(resource: PlayerResourceWireDto): PlayerResource {
  return {
    id: resource.id ?? 0,
    code: resource.code ?? "",
    displayName: resource.displayName ?? "",
    description: resource.description ?? null,
    iconPath: resource.iconPath ?? "",
    isGradedResource: resource.isGradedResource ?? false,
    resourceGrade: resource.resourceGrade ?? "None",
    sortOrder: resource.sortOrder ?? 0,
    neverLuxury: resource.neverLuxury ?? false,
    // Explicitly null rather than 0 when absent: zero would read as "obsoleting this Round", which is the one thing the field never means.
    obsoleteInRounds: resource.obsoleteInRounds ?? null,
    // The seven fields below are contract placeholders the API always sends as zero or false until their own tickets land. A zero here is the
    // absence of a source, not a measured value.
    amount: resource.amount ?? 0,
    amountAddedPerTurn: resource.amountAddedPerTurn ?? 0,
    hasStrategicStockpile: resource.hasStrategicStockpile ?? false,
    isImported: resource.isImported ?? false,
    isExported: resource.isExported ?? false,
    demandNotMet: resource.demandNotMet ?? false,
    isLuxury: resource.isLuxury ?? false,
  };
}

function mapGroup(group: PlayerResourceGroupWireDto): PlayerResourceGroup {
  return {
    id: group.id ?? 0,
    code: group.code ?? "",
    displayName: group.displayName ?? "",
    sortOrder: group.sortOrder ?? null,
    resources: (group.resources ?? []).filter((resource) => resource.id != null).map(mapResource),
  };
}

function mapCategory(category: PlayerResourceCategoryWireDto): PlayerResourceCategory {
  return {
    id: category.id ?? 0,
    code: category.code ?? "",
    displayName: category.displayName ?? "",
    // Standalone is the safer default for a category the client cannot read: it renders a flat grid without needing groups.
    categoryType: category.categoryType ?? "StandaloneResources",
    sortOrder: category.sortOrder ?? 0,
    groups: (category.groups ?? []).filter((group) => group.id != null).map(mapGroup),
    resources: (category.resources ?? []).filter((resource) => resource.id != null).map(mapResource),
  };
}

function mapMetaCategory(metaCategory: PlayerResourceMetaCategoryWireDto): PlayerResourceMetaCategory {
  return {
    id: metaCategory.id ?? 0,
    code: metaCategory.code ?? "",
    displayName: metaCategory.displayName ?? "",
    iconPath: metaCategory.iconPath ?? "",
    sortOrder: metaCategory.sortOrder ?? 0,
    categories: (metaCategory.categories ?? []).filter((category) => category.id != null).map(mapCategory),
  };
}

/**
 * Everything the Resources toolbar and its panels need for the signed-in Player, in one tree. The API reads game and player from the game token.
 *
 * Nothing is re-sorted here. The API returns meta-categories, categories, groups and resources already ordered, so array order is render order —
 * which is also what lets the view-model mapper take a group's grid column straight from its position.
 *
 * Rejects with a 403 when the caller has not entered a game, and a 404 when they are not in an in-progress game or it has no turn queue. Callers
 * should treat both as "nothing unlocked" rather than as a failure worth reporting.
 */
export async function getPlayerResources(): Promise<PlayerResourceMetaCategory[]> {
  const { data } = await http.get<PlayerResourceTreeWireDto>("/player/resources");

  return (data.metaCategories ?? []).filter((metaCategory) => metaCategory.id != null).map(mapMetaCategory);
}
