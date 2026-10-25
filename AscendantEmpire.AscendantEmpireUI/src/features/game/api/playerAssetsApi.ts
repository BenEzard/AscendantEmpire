import { http } from "@/api/http";

/** The API's `AssetGrade` member names. */
export type AssetGradeName = "None" | "Low" | "Medium" | "High";

/** The API's `AssetCategoryType` member names. */
export type AssetCategoryTypeName = "StandaloneAssets" | "GroupedAssets";

/**
 * One Asset the Player can see, as the asset tree reports it.
 *
 * A graded Asset spells its grade out as the last segment of its dotted `code`, because every grade of it shares one `displayName`.
 */
export type PlayerAsset = {
  id: number;
  /** Hierarchical and dotted, e.g. "Weapons.MeleeWeapons.BoneKnife.Low". Unique, and what the panel keys its tickers by. */
  code: string;
  displayName: string;
  description: string | null;
  /** Root-relative URL of the Asset's icon, rendered straight into an image source. Empty when the catalogue has none. */
  iconPath: string;
  isGradedAsset: boolean;
  assetGrade: AssetGradeName;
  sortOrder: number;
  neverLuxury: boolean;
  /**
   * Rounds until this Asset stops being available, or null when nothing is scheduled.
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

/** A row of Assets inside a grouped category — the panel lays one group out per grid column. */
export type PlayerAssetGroup = {
  /** The catalogue row's id. NOT unique across the tree — every per-other-player projection shares its template's id, so key on `code`. */
  id: number;
  code: string;
  /** The group's label, which on a per-other-player projection is the subject player's people name rather than the catalogue's own. */
  displayName: string;
  /**
   * The player whose people this group counts, or null for an ordinary group.
   *
   * A populated value means the group was projected from a per-other-player template. The id is what to key on: the name is the label, not the
   * identity.
   */
  subjectPlayerId: number | null;
  /** Null where the group carries no explicit order, in which case the API has already sorted it after every numbered group and then by name. */
  sortOrder: number | null;
  assets: PlayerAsset[];
};

/**
 * One category under a meta-category.
 *
 * Exactly one of `groups` and `assets` is populated, chosen by `categoryType`. The other is empty rather than null, so either arm can be
 * rendered without a null check.
 */
export type PlayerAssetCategory = {
  /** The catalogue row's id, unique across the tree — the per-other-player fan-out happens a level down, on the groups. */
  id: number;
  code: string;
  displayName: string;
  categoryType: AssetCategoryTypeName;
  sortOrder: number;
  groups: PlayerAssetGroup[];
  assets: PlayerAsset[];
};

/**
 * One unlocked meta-category: a button in the Assets toolbar group, and the panel behind it.
 *
 * `categories` may be empty — an unlocked meta-category with nothing available under it still shows its toolbar icon, over an empty panel.
 */
export type PlayerAssetMetaCategory = {
  id: number;
  code: string;
  displayName: string;
  /** Root-relative URL of the icon shown on this meta-category's toolbar button. Empty when the catalogue has none. */
  iconPath: string;
  sortOrder: number;
  categories: PlayerAssetCategory[];
};

type PlayerAssetWireDto = Partial<PlayerAsset>;

type PlayerAssetGroupWireDto = Partial<Omit<PlayerAssetGroup, "assets"> & { assets: PlayerAssetWireDto[] }>;

type PlayerAssetCategoryWireDto = Partial<
  Omit<PlayerAssetCategory, "groups" | "assets"> & { groups: PlayerAssetGroupWireDto[]; assets: PlayerAssetWireDto[] }
>;

type PlayerAssetMetaCategoryWireDto = Partial<Omit<PlayerAssetMetaCategory, "categories"> & { categories: PlayerAssetCategoryWireDto[] }>;

type PlayerAssetTreeWireDto = Partial<{ metaCategories: PlayerAssetMetaCategoryWireDto[] }>;

/** Reads one Asset off the wire, filling in anything the server left out. Shared by the grouped and standalone arms of a category. */
function mapResource(asset: PlayerAssetWireDto): PlayerAsset {
  return {
    id: asset.id ?? 0,
    code: asset.code ?? "",
    displayName: asset.displayName ?? "",
    description: asset.description ?? null,
    iconPath: asset.iconPath ?? "",
    isGradedAsset: asset.isGradedAsset ?? false,
    assetGrade: asset.assetGrade ?? "None",
    sortOrder: asset.sortOrder ?? 0,
    neverLuxury: asset.neverLuxury ?? false,
    // Explicitly null rather than 0 when absent: zero would read as "obsoleting this Round", which is the one thing the field never means.
    obsoleteInRounds: asset.obsoleteInRounds ?? null,
    // A measured figure: summed across the player's asset banks for a banked asset, and the unspent directed research points for Technology
    // experience. A banked asset the player holds none of never arrives, because the API leaves it out of the tree, so a zero here is a
    // player-level or a population figure - and those read zero while their stores are still being built.
    amount: asset.amount ?? 0,
    // The six fields below are contract placeholders the API always sends as zero or false until their own tickets land. A zero here is the
    // absence of a source, not a measured value.
    amountAddedPerTurn: asset.amountAddedPerTurn ?? 0,
    hasStrategicStockpile: asset.hasStrategicStockpile ?? false,
    isImported: asset.isImported ?? false,
    isExported: asset.isExported ?? false,
    demandNotMet: asset.demandNotMet ?? false,
    isLuxury: asset.isLuxury ?? false,
  };
}

function mapGroup(group: PlayerAssetGroupWireDto): PlayerAssetGroup {
  return {
    id: group.id ?? 0,
    code: group.code ?? "",
    displayName: group.displayName ?? "",
    // Null rather than 0 when absent: 0 is not a player id, and an ordinary group legitimately has no subject.
    subjectPlayerId: group.subjectPlayerId ?? null,
    sortOrder: group.sortOrder ?? null,
    assets: (group.assets ?? []).filter((asset) => asset.id != null).map(mapResource),
  };
}

function mapCategory(category: PlayerAssetCategoryWireDto): PlayerAssetCategory {
  return {
    id: category.id ?? 0,
    code: category.code ?? "",
    displayName: category.displayName ?? "",
    // Standalone is the safer default for a category the client cannot read: it renders a flat grid without needing groups.
    categoryType: category.categoryType ?? "StandaloneAssets",
    sortOrder: category.sortOrder ?? 0,
    groups: (category.groups ?? []).filter((group) => group.id != null).map(mapGroup),
    assets: (category.assets ?? []).filter((asset) => asset.id != null).map(mapResource),
  };
}

function mapMetaCategory(metaCategory: PlayerAssetMetaCategoryWireDto): PlayerAssetMetaCategory {
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
 * Everything the Assets toolbar and its panels need for the signed-in Player, in one tree. The API reads game and player from the game token.
 *
 * Nothing is re-sorted here. The API returns meta-categories, categories, groups and assets already ordered, so array order is render order —
 * which is also what lets the view-model mapper take a group's grid column straight from its position.
 *
 * Rejects with a 403 when the caller has not entered a game, and a 404 when they are not in an in-progress game or it has no turn queue. Callers
 * should treat both as "nothing unlocked" rather than as a failure worth reporting.
 */
export async function getPlayerAssets(): Promise<PlayerAssetMetaCategory[]> {
  const { data } = await http.get<PlayerAssetTreeWireDto>("/player/assets");

  return (data.metaCategories ?? []).filter((metaCategory) => metaCategory.id != null).map(mapMetaCategory);
}
