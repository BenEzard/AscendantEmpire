import type { Biome } from "@/types/map/Biome";
import type { Geography } from "@/types/map/Geography";

const BIOME_DEFAULTS = {
  // A new biome starts with no sustenance values, and the server requires the rate to be empty until it has one.
  regenerationRate: null,
  movementCost: 1,
  isTraversable: true,
  isWater: false,
  isPaintableOnCampaignMap: true,
  doesBlockSight: false,
} as const;

export function getFirstBiomeType(list: Biome[]): number | null {
  return list
    .slice()
    .sort((a, b) => a.name.localeCompare(b.name))[0]
    ?.biomeType ?? null;
}

export function getNextBiomeTypeId(list: Biome[]): number {
  return Math.max(0, ...list.map((b) => b.biomeType)) + 1;
}

export function createNewBiome(nextId: number): Biome {
  return {
    biomeType: nextId,
    name: "new",
    isTraversable: BIOME_DEFAULTS.isTraversable,
    movementCost: BIOME_DEFAULTS.movementCost,
    isWater: BIOME_DEFAULTS.isWater,
    doesBlockSight: BIOME_DEFAULTS.doesBlockSight,
    canBeElevated: false,
    isPaintableOnCampaignMap: BIOME_DEFAULTS.isPaintableOnCampaignMap,
    mapEditorHtmlColor: "",
    regenerationRate: BIOME_DEFAULTS.regenerationRate,
    baseForageValue: null,
    baseHuntValue: null,
    baseFishValue: null,
    baseFodderValue: null,
    baseBoneValue: null,
    baseWoodValue: null,
    baseStoneValue: null,
    baseSpecialValue: null,
  };
}

const GEO_DEFAULTS = {
  relativeElevation: 0,
  movementCost: 1,
  isPaintableOnCampaignMap: true,
} as const;

export function getFirstGeographyType(list: Geography[]): number | null {
  return list
    .slice()
    .sort((a, b) => a.name.localeCompare(b.name))[0]
    ?.campaignMapGeographyType ?? null;
}

export function getNextGeographyTypeId(list: Geography[]): number {
  return Math.max(0, ...list.map((g) => g.campaignMapGeographyType)) + 1;
}

export function createNewGeography(nextId: number): Geography {
  return {
    campaignMapGeographyType: nextId,
    name: "new",
    relativeElevation: GEO_DEFAULTS.relativeElevation,
    movementCost: GEO_DEFAULTS.movementCost,
    mapEditorIcon: null,
    isPaintableOnCampaignMap: GEO_DEFAULTS.isPaintableOnCampaignMap,
  };
}
