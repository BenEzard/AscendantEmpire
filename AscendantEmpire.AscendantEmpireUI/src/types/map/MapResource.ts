// The MapResource catalogue as the API sends and receives it (GET/PUT /map-resources/catalogue).
//
// Mirrors IMapResourceCatalogue / IAddOrUpdateMapResourceTypeDto. Quantities are whole numbers (the server stores them scaled by
// 1,000); likelihoods and percentages are fractions (0.25 = 25%) — the manager shows those as percents. See
// docs/design/campaign-map/map-resources.md.
//
// A habitat's Biome and Geography are enums, and so arrive as their member names — see BiomeType and CampaignMapGeographyType.

import type { BiomeType } from "./Biome";
import type { CampaignMapGeographyType } from "./Geography";

/**
 * How a MapResource is extracted. The values are the C# `ExtractionMethod` enum's member *names*, not its numbers, because the API serialises
 * enums as their name (AscendantEmpire.Api/Program.cs registers a JsonStringEnumConverter).
 *
 * Kept as a named lookup rather than a bare union so the code reads `ExtractionMethod.Capture` at the one place that singles a method out.
 */
export const ExtractionMethod = {
  Gather: "Gather",
  Hunt: "Hunt",
  Fell: "Fell",
  Mine: "Mine",
  Capture: "Capture",
} as const;

export type ExtractionMethod = (typeof ExtractionMethod)[keyof typeof ExtractionMethod];

/** One Asset a MapResourceType gives, and how much one unit extracted gives. */
export type MapResourceTypeYield = {
  assetId: number;
  amountPerUnit: number;
  isEnabledByDefault: boolean; // Off until a game mechanic switches it on for a Player (e.g. a Deer's pelt).
};

/** Where a MapResourceType can exist (a Biome, optionally one Geography in it) and its values there. */
export type MapResourceHabitat = {
  biomeType: BiomeType;
  geographyType: CampaignMapGeographyType | null; // null = every Geography in the Biome.
  initialQuantityMin: number | null;
  initialQuantityMax: number | null;
  maximumQuantity: number | null;
  growthPercentage: number | null;
  spreadLikelihood: number | null;
  spreadLimitMin: number | null;
  spreadLimitMax: number | null;
};

/** One set of starting amounts a Distribution is created from, and its weight in each Biome. */
export type MapResourceDistributionScenario = {
  scenarioNumber: number;
  displayName: string | null;
  amounts: { assetId: number; amount: number }[];
  biomeWeights: { biomeType: BiomeType; weight: number }[];
};

/** One layer of a stratified MapResourceType. */
export type MapResourceTypeLayer = {
  layerNumber: number;
  assetId: number;
  emptyLikelihood: number;
  quantityMin: number;
  quantityMax: number;
  discoveryLikelihood: number | null; // null on layer 1, which uses the type's own.
  requiredMineLevel: number | null; // null on layer 1.
  estimationFuzzyFactorMin: number;
  estimationFuzzyFactorMax: number;
};

/** A MapResourceType with everything it owns. `id` is absent until the type has been saved. */
export type MapResourceType = {
  id?: number;
  code: string;
  displayName: string;
  mapResourceCategoryCode: string;
  sortOrder: number;
  iconPath: string | null; // Image drawn on the map tile; null = no icon.

  isRenewable: boolean;
  isStratified: boolean;
  isDistribution: boolean;

  // Not a kind: it narrows which Biomes and Geographies the Habitats page offers, and which the server will accept.
  isWaterResource: boolean;

  extractionMethod: ExtractionMethod;
  captureLikelihood: number | null;
  requiresResearchToExtract: boolean;

  isDiscoverableByDefault: boolean;
  discoveryLikelihood: number;
  estimationFuzzyFactorMin: number | null;
  estimationFuzzyFactorMax: number | null;

  survivalRounds: number | null;
  reseedLikelihood: number | null;

  yields: MapResourceTypeYield[];
  habitats: MapResourceHabitat[];
  distributionScenarios: MapResourceDistributionScenario[];
  layers: MapResourceTypeLayer[];
};

/** A MapResourceCategory as the GET returns it, with its types. `id` is absent until saved. */
export type MapResourceCategory = {
  id?: number;
  code: string;
  displayName: string;
  sortOrder: number;
  types: MapResourceType[];
};

/** An Asset, as listed for the yield, scenario and layer pickers (GET /assets). */
export type AssetSummary = {
  id: number;
  code: string;
  displayName: string;
  metaCategoryDisplayName: string;
  isNaturallyOccurring: boolean;
};
