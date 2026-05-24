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
    resourcesAddedPerTurn: options?.resourcesAddedPerTurn,
    description: options?.description,
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
    resourcesAddedPerTurn: resourceDef.resourcesAddedPerTurn,
    description: resourceDef.description,
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
          resource("Oak Log", 2400, { description: "A dense, slow-grown hardwood felled from ancient oak forests. Renowned for its strength and resistance to rot, oak logs are a cornerstone of construction and military engineering alike.", tooltip: "Oak Log", icon: imageIcon(oakLogIconUrl), hasStrategicStockpile: true }),
          resource("Oak Plank", 1260, { description: "Milled from prime oak logs, these planks provide reliable structural integrity for buildings, warships, and siege equipment. Widely traded across the empire.", tooltip: "Oak Plank: 1260", icon: imageIcon(oakPlankIconUrl), isExported: true }),
        ],
      },
      {
        label: "Generic Wood",
        tickers: [
          resource("Generic Log", 2400, { description: "Logs of mixed or unspecified timber, sourced from managed woodland reserves. Serviceable for fuel, basic construction, and rough carpentry.", tooltip: "Generic Log", icon: imageIcon(pineLogIconUrl), isImported: true }),
          resource("Generic Plank", 360, {
            description: "Planks cut from common-grade timber. Adequate for everyday construction needs, though less prized than specialty hardwoods.",
            tooltip: "Generic Plank: 360",
            icon: imageIcon(pinePlankIconUrl),
            soonToBeObsolete: true,
          }),
        ],
      },
      {
        tickers: [
          resource("Cedar Log", 999, { description: "A fragrant, lightweight softwood prized for its natural resistance to insects and moisture. Cedar logs are favored for interior fittings and storage chests.", tooltip: "Cedar Log", icon: imageIcon(cedarLogIconUrl) }),
          resource("Cedar Plank", 0, { description: "Planks milled from cedar logs. Lightweight and aromatic, they are popular for lining storage facilities and crafting furniture.", tooltip: "Cedar Plank: 0", icon: imageIcon(cedarPlankIconUrl) }),
        ],
      },
      {
        tickers: [
          resource("Ash Log", 9900, { resourcesAddedPerTurn: 10000, description: "A flexible, tough hardwood cut from ash groves. Ash logs are particularly valued by weaponsmiths and wheelwrights for their shock-absorbing properties.", tooltip: "Ash Log", icon: imageIcon(ashLogIconUrl) }),
          resource("Ash Plank", 0, { description: "Planks produced from ash logs. Used in the production of tool handles, weapon shafts, and lightweight vehicle components.", tooltip: "Ash Plank: 0", icon: imageIcon(ashPlankIconUrl), isImported: true }),
        ],
      },
      {
        tickers: [
          resource("Ebony Log", 12, { description: "An exceptionally rare and heavy tropical hardwood. Ebony logs command premium prices on the open market and are reserved for luxury goods and high-status construction.", tooltip: "Ebony Log", icon: imageIcon(ebonyLogIconUrl), isLuxury: true }),
          resource("Ebony Plank", 1, { description: "Finely milled planks of ebony. Sought after by master craftsmen for decorative inlays, fine furniture, and prestigious buildings.", tooltip: "Ebony Plank: 1", icon: imageIcon(ebonyPlankIconUrl), isLuxury: true }),
        ],
      },
      {
        tickers: [resource("Bamboo", 500, { description: "A fast-regenerating grass used as a versatile construction material. Bamboo is lightweight, surprisingly strong, and grows abundantly in tropical regions.", tooltip: "Bamboo", icon: imageIcon(bambooIconUrl), isExported: true })],
      },
    ],
  }),
  createProcessedCategory({
    key: "stone",
    label: "Stone",
    column: 2,
    icon: createElement(GiStoneBlock),
    groups: [
      { tickers: [resource("Limestone", 10_000, { description: "A sedimentary rock abundant throughout the empire. Limestone is the foundation of basic construction and is crushed to produce mortar and quicklime.", hasStrategicStockpile: true })] },
      {
        tickers: [
          resource("Sandstone", 15_500, { description: "A soft, workable stone gathered from arid regions. Easier to cut than granite, sandstone is commonly used in desert architecture.", icon: imageIcon(sandstoneRocksIconUrl) }),
          resource("Sandstone Block", 1_280, { description: "Dressed sandstone blocks shaped for construction. Widely used in the southern provinces for walls, temples, and public buildings.", icon: imageIcon(sandstoneBlockIconUrl) }),
        ],
      },
      { tickers: [resource("Granite", 20_000, { description: "An exceptionally hard igneous rock quarried from mountain deposits. Granite resists weathering and is ideal for fortifications and public monuments." })] },
      { tickers: [resource("Basalt", 45_000, { description: "A dense volcanic rock with superior hardness. Basalt is prized for road paving, millstones, and heavy defensive structures.", demandNotMet: true, isImported: true })] },
      { tickers: [resource("Generic Rock", 75_000, { description: "Rough, unprocessed stone quarried from surface deposits. Used as bulk fill, rubble, and a basic ingredient in early-stage construction.", icon: imageIcon(genericRockPileIconUrl) })] },
      {
        tickers: [
          resource("Marble", 3_500, { description: "Raw marble extracted from prized quarries. Highly sought after for prestige architecture, sculptures, and civic monuments.", isLuxury: true, isExported: true, icon: imageIcon(marblePileIconUrl) }),
          resource("Marble Block", 420, { description: "Cut and dressed marble blocks, ready for use in grand construction projects. A symbol of wealth and refined taste.", isLuxury: true, icon: imageIcon(marbleBlockIconUrl) }),
        ],
      },
      { tickers: [resource("Flint", 1_200, { description: "A fine-grained sedimentary rock that fractures with a sharp edge. Flint is used in early toolmaking, fire-starting, and as a cheap cutting material." })] },
      { tickers: [resource("Obsidian", 420, { description: "Volcanic glass formed from rapidly cooled lava. Obsidian is razor-sharp when fractured and is prized for specialist blades and ritual objects.", isLuxury: true, icon: imageIcon(obsidianPileIconUrl) })] },
      { tickers: [resource("Slate", 8_000, { description: "A fine-grained metamorphic rock that cleaves into thin sheets. Slate is the material of choice for roofing tiles and writing tablets." })] },
    ],
  }),
];

const standaloneCategory = createStandaloneCategory({
  key: "other",
  label: "Other",
  column: 3,
  icon: createElement(GiClayBrick),
  resources: [
    resource("Bone", 500, { description: "Bones collected from slaughterhouses and hunting camps. Processed into tools, needles, fertiliser, and animal glue.", icon: imageIcon(boneIconUrl), hasStrategicStockpile: true }),
    resource("Fodder", 42, { description: "Dried grasses, hay, and grain husks used to sustain livestock and working animals. A steady supply is essential for any mounted force or agricultural operation.", icon: imageIcon(grassIconUrl), isImported: true }),
    resource("Ochre", 650, { description: "A naturally occurring iron oxide pigment. Ochre is used in dyes, paints, and pottery glazes throughout the empire." }),
    resource("Chalk", 2_250, { description: "A soft white limestone used as a writing material, in agriculture to neutralise acidic soils, and as a base for certain pigments." }),
    resource("Clay", 3_100, { description: "Raw earthen material gathered from riverbeds and mineral deposits. Clay is the primary ingredient in pottery, bricks, and ceramic tiles." }),
    resource("Salt", 980, { description: "A vital mineral harvested from salt flats and coastal evaporation pools. Salt is essential for food preservation, leather tanning, and chemical processes.", isExported: true }),
    resource("Sulfur", 120, { description: "A yellow mineral collected from volcanic vents and mineral springs. Sulfur is a key ingredient in gunpowder, medicinal compounds, and fumigation." }),
    resource("Pitch", 440, { description: "A thick, dark tar extracted from pine trees or coal deposits. Pitch waterproofs ships and barrels, seals building joints, and fuels incendiary weapons." }),
    resource("Flax", 1_720, { description: "A fibrous plant cultivated for its stems and seeds. Flax is processed into linen cloth, rope, and linseed oil." }),
    resource("Resin", 375, { description: "Sticky sap harvested from conifer trees. Resin is used in varnishes, adhesives, medicines, and as a flux in metalworking." }),
    resource("Tar", 810, { description: "A dense, viscous liquid derived from the destructive distillation of wood or coal. Tar preserves timber, waterproofs hulls, and serves as a base for road surfaces." }),
    resource("Leather", 1_180, { description: "Cured animal hides processed into a durable and flexible material. Leather is indispensable for armour, harnesses, footwear, and bookbinding." }),
    resource("Hides", 640, { description: "Raw, uncured animal skins awaiting processing. Hides are the primary input for the leather-working industry." }),
    resource("Tallow", 260, { description: "Animal fat rendered from slaughtered livestock. Tallow is used in candle-making, soap production, and as a lubricant for machinery and weapons." }),
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
        bronze: resource("Bone Knife_Bronze", 5, { description: "A standard-issue bone knife issued to basic levy troops. Cheap to produce and easy to replace, though barely adequate for combat.", grade: "bronze", isImported: true }),
        silver: resource("Bone Knife_Silver", 1200, { description: "A refined bone knife with improved craftsmanship. The bone has been carefully selected and shaped for better durability.", grade: "silver" }),
        gold: resource("Bone Knife_Gold", 0, { description: "A masterwork bone knife, treated and hardened through specialist techniques. Surprisingly effective for such a primitive material.", grade: "gold" }),
      },
    },
    {
      label: "Wood Club",
      icon: imageIcon(woodClubIconUrl),
      resourcesByGrade: {
        bronze: resource("Wood Club_Bronze", 11, { description: "A rough bludgeoning weapon cut from dense hardwood. One of the oldest weapons known, the wood club remains a staple of poorly-equipped forces.", grade: "bronze", hasStrategicStockpile: true }),
        silver: resource("Wood Club_Silver", 3, { description: "A shaped and balanced wood club with a reinforced striking head. More effective than a crude branch and easier to wield in close quarters.", grade: "silver" }),
        gold: resource("Wood Club_Gold", 1, { description: "A masterwork wood club, weighted and finished with metal bands. A surprisingly formidable weapon in skilled hands.", grade: "gold" }),
      },
    },
    {
      label: "Two-Piece Wood Spear",
      icon: imageIcon(woodSpearIconUrl),
      resourcesByGrade: {
        bronze: resource("Two-Piece Wood Spear_Bronze", 8, { description: "A basic two-section thrusting weapon assembled from shaped wooden shafts. The modular design allows easy transport and field repair.", grade: "bronze" }),
        silver: resource("Two-Piece Wood Spear_Silver", 6, { description: "A well-balanced two-piece spear with a hardened tip and fitted joints. Reliable in formation combat and light skirmishing.", grade: "silver", isExported: true }),
        gold: resource("Two-Piece Wood Spear_Gold", 1, { description: "A masterwork two-piece spear with precision fittings and a reinforced head. Prized by elite skirmishers for its reach and handling.", grade: "gold" }),
      },
    },
    {
      label: "Stone Axe",
      icon: imageIcon(stoneAxeIconUrl),
      resourcesByGrade: {
        bronze: resource("Stone Axe_Bronze", 4, { description: "A heavy chopping weapon with a rough stone head bound to a wooden handle. Predates metalworking and still found where iron is scarce.", grade: "bronze" }),
        silver: resource("Stone Axe_Silver", 4, { description: "A shaped and polished stone axe with a well-fitted haft. More reliable than crude versions, though being phased out in favour of metal weapons.", grade: "silver", soonToBeObsolete: true }),
        gold: resource("Stone Axe_Gold", 0, { description: "A prestige stone axe crafted with exceptional skill. A collector's piece as much as a weapon, rarely produced in the current age.", grade: "gold" }),
      },
    },
    {
      label: "Medieval Sword",
      icon: imageIcon(medievalSwordIconUrl),
      resourcesByGrade: {
        bronze: resource("Medieval Sword_Bronze", 2, { description: "A straight-bladed cutting and thrusting weapon of sophisticated design. Requires skilled smiths and quality materials, marking it as a step above primitive arms.", grade: "bronze" }),
        silver: resource("Medieval Sword_Silver", 4, { description: "A finely balanced sword with quality steel and a well-finished guard. The weapon of choice for professional soldiers and officers.", grade: "silver", isImported: true }),
        gold: resource("Medieval Sword_Gold", 0, { description: "A masterwork sword of exceptional quality, fit for a champion. Demand consistently outstrips the empire's ability to produce them.", grade: "gold", demandNotMet: true }),
      },
    },
  ],
});

export const resourceDummyData: ResourceCategoryData[] = [...processedCategories, standaloneCategory, manufacturedCategory];