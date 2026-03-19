import { createElement, type ReactNode } from "react";
import { GiClayBrick, GiStoneBlock, GiWoodStick } from "react-icons/gi";

import boneIconUrl from "@/assets/other/fishbone.svg";
import grassIconUrl from "@/assets/other/high-grass.svg";
import ashLogIconUrl from "@/assets/wood/ash-log_40px.png";
import ashPlankIconUrl from "@/assets/wood/ash-plank_40px.png";
import bambooIconUrl from "@/assets/wood/bamboo_40px.png";
import cedarLogIconUrl from "@/assets/wood/cedar-log_40px.png";
import cedarPlankIconUrl from "@/assets/wood/cedar-plank_40px.png";
import ebonyLogIconUrl from "@/assets/wood/ebony-log_40px.png";
import ebonyPlankIconUrl from "@/assets/wood/ebony-plank_40px.png";
import oakLogIconUrl from "@/assets/wood/oak-log_40px.png";
import oakPlankIconUrl from "@/assets/wood/oak-plank_40px.png";
import pineLogIconUrl from "@/assets/wood/pine-log_40px.png";
import pinePlankIconUrl from "@/assets/wood/pine-plank_40px.png";
import genericRockPileIconUrl from "@/assets/stone/generic-rock-pile_40px.png";
import marbleBlockIconUrl from "@/assets/stone/marble-block_40px.png";
import marblePileIconUrl from "@/assets/stone/marble-pile_40px.png";
import obsidianPileIconUrl from "@/assets/stone/obsidian-pile_40px.png";
import sandstoneBlockIconUrl from "@/assets/stone/sandstone-block_40px.png";
import sandstoneRocksIconUrl from "@/assets/stone/sandstone-rocks_40px.png";
import boneKnifeIconUrl from "@/assets/unit-design/weapons/melee/bone-knife_40px.png";
import medievalSwordIconUrl from "@/assets/unit-design/weapons/melee/medieval-sword_40px.png";
import stoneAxeIconUrl from "@/assets/unit-design/weapons/melee/stone-axe_40px.png";
import woodClubIconUrl from "@/assets/unit-design/weapons/melee/wood-club_40px.png";
import woodSpearIconUrl from "@/assets/unit-design/weapons/melee/two-piece-wood-spear_40px.png";
import type {
  ManufacturedGradesCategoryData,
  ManufacturedResourceCollectionData,
  ProcessedResourceCategoryData,
  ResourceCategoryData,
  ResourceTickerData,
  ResourceTickerGroupData,
  StandaloneResourceCategoryData,
} from "./ResourceTickerCollection";
import { MANUFACTURED_RESOURCE_GRADES, type ManufacturedResourceGrade } from "./ResourceConstants";

type ResourceDefinition = Omit<ResourceTickerData, "id" | "name"> & {
  label: string;
};

type ResourceGroupDefinition = {
  label?: string;
  tickers: ResourceDefinition[];
};

type ManufacturedCollectionDefinition = {
  label: string;
  icon: ReactNode;
  resourcesByGrade: Record<ManufacturedResourceGrade, ResourceDefinition>;
};

function imageIcon(src: string): ReactNode {
  return createElement("img", { src, alt: "" });
}

function resource(label: string, amount: number, options?: Omit<ResourceDefinition, "label" | "amount">): ResourceDefinition {
  return {
    label,
    amount,
    tooltip: options?.tooltip,
    icon: options?.icon,
    isLuxury: options?.isLuxury,
    isImported: options?.isImported,
    isExported: options?.isExported,
    hasStrategicStockpile: options?.hasStrategicStockpile,
    soonToBeObsolete: options?.soonToBeObsolete,
    demandNotMet: options?.demandNotMet,
    grade: options?.grade,
  };
}

function createTickerId(categoryKey: string, label: string) {
  return `${categoryKey}-${label.toLowerCase().replaceAll(" ", "-").replaceAll("_", "-")}`;
}

function createTicker(categoryKey: string, resourceDef: ResourceDefinition, column?: number): ResourceTickerData {
  return {
    id: createTickerId(categoryKey, resourceDef.label),
    name: resourceDef.label,
    column,
    amount: resourceDef.amount,
    tooltip: resourceDef.tooltip,
    icon: resourceDef.icon,
    isLuxury: resourceDef.isLuxury === true,
    isImported: resourceDef.isImported,
    isExported: resourceDef.isExported,
    hasStrategicStockpile: resourceDef.hasStrategicStockpile,
    soonToBeObsolete: resourceDef.soonToBeObsolete,
    demandNotMet: resourceDef.demandNotMet,
    grade: resourceDef.grade,
  };
}

function createTickerGroup(categoryKey: string, group: ResourceGroupDefinition, column: number): ResourceTickerGroupData {
  const tickers = group.tickers.map((ticker, rowIndex) => createTicker(categoryKey, ticker, rowIndex + 1));

  return {
    id: `${categoryKey}-${(group.label ?? group.tickers[0]?.label ?? `group-${column}`).toLowerCase().replaceAll(" ", "-")}`,
    label: group.label ?? group.tickers[0]?.label ?? `Group ${column}`,
    column,
    tickers,
  };
}

function createProcessedCategory(definition: {
  key: string;
  label: string;
  column: number;
  icon: ReactNode;
  groups: ResourceGroupDefinition[];
  showTotal?: boolean;
}): ProcessedResourceCategoryData {
  return {
    key: definition.key,
    type: "processed-resources",
    label: definition.label,
    column: definition.column,
    icon: definition.icon,
    showTotal: definition.showTotal,
    tickerGroups: definition.groups.map((group, groupIndex) => createTickerGroup(definition.key, group, groupIndex + 1)),
  };
}

function createStandaloneCategory(definition: {
  key: string;
  label: string;
  column: number;
  icon: ReactNode;
  resources: ResourceDefinition[];
}): StandaloneResourceCategoryData {
  return {
    key: definition.key,
    type: "standalone-resources",
    label: definition.label,
    column: definition.column,
    icon: definition.icon,
    resources: definition.resources.map((item) => createTicker(definition.key, item)),
  };
}

function createManufacturedCollection(categoryKey: string, definition: ManufacturedCollectionDefinition): ManufacturedResourceCollectionData {
  const normalizedLabel = definition.label.toLowerCase().replaceAll(" ", "-");
  const resourcesByGrade = Object.fromEntries(
    MANUFACTURED_RESOURCE_GRADES.map((grade) => [grade, createTicker(categoryKey, definition.resourcesByGrade[grade])]),
  ) as Record<ManufacturedResourceGrade, ResourceTickerData>;

  return {
    id: `${categoryKey}-${normalizedLabel}`,
    label: definition.label,
    icon: definition.icon,
    resourcesByGrade,
  };
}

function createManufacturedCategory(definition: {
  key: string;
  label: string;
  column: number;
  icon: ReactNode;
  collections: ManufacturedCollectionDefinition[];
}): ManufacturedGradesCategoryData {
  return {
    key: definition.key,
    type: "manufactured-grades",
    label: definition.label,
    column: definition.column,
    icon: definition.icon,
    collections: definition.collections.map((collection) => createManufacturedCollection(definition.key, collection)),
  };
}

const processedCategories: ProcessedResourceCategoryData[] = [
  createProcessedCategory({
    key: "wood",
    label: "Wood",
    column: 1,
    icon: createElement(GiWoodStick),
    groups: [
      {
        tickers: [
          resource("Oak Log", 2400, { tooltip: "Oak Log", icon: imageIcon(oakLogIconUrl), hasStrategicStockpile: true }),
          resource("Oak Plank", 1260, { tooltip: "Oak Plank: 1260", icon: imageIcon(oakPlankIconUrl), isExported: true }),
        ],
      },
      {
        label: "Generic Wood",
        tickers: [
          resource("Generic Log", 2400, { tooltip: "Generic Log", icon: imageIcon(pineLogIconUrl), isImported: true }),
          resource("Generic Plank", 360, {
            tooltip: "Generic Plank: 360",
            icon: imageIcon(pinePlankIconUrl),
            soonToBeObsolete: true,
          }),
        ],
      },
      {
        tickers: [
          resource("Cedar Log", 999, { tooltip: "Cedar Log", icon: imageIcon(cedarLogIconUrl) }),
          resource("Cedar Plank", 0, { tooltip: "Cedar Plank: 0", icon: imageIcon(cedarPlankIconUrl) }),
        ],
      },
      {
        tickers: [
          resource("Ash Log", 9900, { tooltip: "Ash Log", icon: imageIcon(ashLogIconUrl) }),
          resource("Ash Plank", 0, { tooltip: "Ash Plank: 0", icon: imageIcon(ashPlankIconUrl), isImported: true }),
        ],
      },
      {
        tickers: [
          resource("Ebony Log", 12, { tooltip: "Ebony Log", icon: imageIcon(ebonyLogIconUrl), isLuxury: true }),
          resource("Ebony Plank", 1, { tooltip: "Ebony Plank: 1", icon: imageIcon(ebonyPlankIconUrl), isLuxury: true }),
        ],
      },
      {
        tickers: [resource("Bamboo", 500, { tooltip: "Bamboo", icon: imageIcon(bambooIconUrl), isExported: true })],
      },
    ],
  }),
  createProcessedCategory({
    key: "stone",
    label: "Stone",
    column: 2,
    icon: createElement(GiStoneBlock),
    groups: [
      { tickers: [resource("Limestone", 10_000, { hasStrategicStockpile: true })] },
      {
        tickers: [
          resource("Sandstone", 15_500, { icon: imageIcon(sandstoneRocksIconUrl) }),
          resource("Sandstone Block", 1_280, { icon: imageIcon(sandstoneBlockIconUrl) }),
        ],
      },
      { tickers: [resource("Granite", 20_000)] },
      { tickers: [resource("Basalt", 45_000, { demandNotMet: true, isImported: true })] },
      { tickers: [resource("Generic Rock", 75_000, { icon: imageIcon(genericRockPileIconUrl) })] },
      {
        tickers: [
          resource("Marble", 3_500, { isLuxury: true, isExported: true, icon: imageIcon(marblePileIconUrl) }),
          resource("Marble Block", 420, { isLuxury: true, icon: imageIcon(marbleBlockIconUrl) }),
        ],
      },
      { tickers: [resource("Flint", 1_200)] },
      { tickers: [resource("Obsidian", 420, { isLuxury: true, icon: imageIcon(obsidianPileIconUrl) })] },
      { tickers: [resource("Slate", 8_000)] },
    ],
  }),
];

const standaloneCategory = createStandaloneCategory({
  key: "other",
  label: "Other",
  column: 3,
  icon: createElement(GiClayBrick),
  resources: [
    resource("Bone", 500, { icon: imageIcon(boneIconUrl), hasStrategicStockpile: true }),
    resource("Fodder", 42, { icon: imageIcon(grassIconUrl), isImported: true }),
    resource("Ochre", 650),
    resource("Chalk", 2_250),
    resource("Clay", 3_100),
    resource("Salt", 980, { isExported: true }),
    resource("Sulfur", 120),
    resource("Pitch", 440),
    resource("Flax", 1_720),
    resource("Resin", 375),
    resource("Tar", 810),
    resource("Leather", 1_180),
    resource("Hides", 640),
    resource("Tallow", 260),
  ],
});

const manufacturedCategory = createManufacturedCategory({
  key: "melee-weapons",
  label: "Melee Weapons",
  column: 4,
  icon: createElement(GiWoodStick),
  collections: [
    {
      label: "Bone Knife",
      icon: imageIcon(boneKnifeIconUrl),
      resourcesByGrade: {
        bronze: resource("Bone Knife_Bronze", 5, { grade: "bronze", isImported: true }),
        silver: resource("Bone Knife_Silver", 1200, { grade: "silver" }),
        gold: resource("Bone Knife_Gold", 0, { grade: "gold" }),
      },
    },
    {
      label: "Wood Club",
      icon: imageIcon(woodClubIconUrl),
      resourcesByGrade: {
        bronze: resource("Wood Club_Bronze", 11, { grade: "bronze", hasStrategicStockpile: true }),
        silver: resource("Wood Club_Silver", 3, { grade: "silver" }),
        gold: resource("Wood Club_Gold", 1, { grade: "gold" }),
      },
    },
    {
      label: "Two-Piece Wood Spear",
      icon: imageIcon(woodSpearIconUrl),
      resourcesByGrade: {
        bronze: resource("Two-Piece Wood Spear_Bronze", 8, { grade: "bronze" }),
        silver: resource("Two-Piece Wood Spear_Silver", 6, { grade: "silver", isExported: true }),
        gold: resource("Two-Piece Wood Spear_Gold", 1, { grade: "gold" }),
      },
    },
    {
      label: "Stone Axe",
      icon: imageIcon(stoneAxeIconUrl),
      resourcesByGrade: {
        bronze: resource("Stone Axe_Bronze", 4, { grade: "bronze" }),
        silver: resource("Stone Axe_Silver", 4, { grade: "silver", soonToBeObsolete: true }),
        gold: resource("Stone Axe_Gold", 0, { grade: "gold" }),
      },
    },
    {
      label: "Medieval Sword",
      icon: imageIcon(medievalSwordIconUrl),
      resourcesByGrade: {
        bronze: resource("Medieval Sword_Bronze", 2, { grade: "bronze" }),
        silver: resource("Medieval Sword_Silver", 4, { grade: "silver", isImported: true }),
        gold: resource("Medieval Sword_Gold", 0, { grade: "gold", demandNotMet: true }),
      },
    },
  ],
});

export const resourceDummyData: ResourceCategoryData[] = [...processedCategories, standaloneCategory, manufacturedCategory];