import { createElement, type ReactNode } from "react";
import { GiClayBrick, GiStoneBlock, GiWoodStick } from "react-icons/gi";

import boneIconUrl from "@/assets/other/fishbone.svg";
import ashLogIconUrl from "@/assets/wood/ash-log_40px.png";
import ashPlankIconUrl from "@/assets/wood/ash-plank_40px.png";
import cedarLogIconUrl from "@/assets/wood/cedar-log_40px.png";
import cedarPlankIconUrl from "@/assets/wood/cedar-plank_40px.png";
import grassIconUrl from "@/assets/other/high-grass.svg";
import pineLogIconUrl from "@/assets/wood/pine-log_40px.png";
import pinePlankIconUrl from "@/assets/wood/pine-plank_40px.png";
import bambooIconUrl from "@/assets/wood/bamboo_40px.png";
import ebonyLogIconUrl from "@/assets/wood/ebony-log_40px.png";
import ebonyPlankIconUrl from "@/assets/wood/ebony-plank_40px.png";
import oakLogIconUrl from "@/assets/wood/oak-log_40px.png";
import oakPlankIconUrl from "@/assets/wood/oak-plank_40px.png";
import type { ResourceCategoryData, ResourceTickerData } from "./ResourceTickerCollection";

type ResourceDefinition = {
  label: string;
  amount: number;
  isLuxury?: boolean;
  tooltip?: string;
  icon?: ReactNode;
};

type ResourceGroupDefinition = {
  label?: string;
  tickers: ResourceDefinition[];
};

type ResourceCategoryDefinition = {
  key: string;
  label: string;
  column: number;
  icon: ReturnType<typeof createElement>;
  mixedItems?: boolean;
  groups: ResourceGroupDefinition[];
};

function resource(label: string, amount: number, options?: Omit<ResourceDefinition, "label" | "amount">): ResourceDefinition {
  return {
    label,
    amount,
    isLuxury: options?.isLuxury,
    tooltip: options?.tooltip,
    icon: options?.icon,
  };
}

function imageIcon(src: string): ReactNode {
  return createElement("img", { src, alt: "" });
}

function createTicker(resourceDef: ResourceDefinition, column: number): ResourceTickerData {
  return {
    id: resourceDef.label.toLowerCase().replaceAll(" ", "-"),
    name: resourceDef.label,
    column,
    amount: resourceDef.amount,
    isLuxury: resourceDef.isLuxury === true,
    tooltip: resourceDef.tooltip,
    icon: resourceDef.icon,
  };
}

function createTickerGroup(categoryKey: string, group: ResourceGroupDefinition, column: number) {
  const tickers = group.tickers.map((ticker, rowIndex) => ({
    ...createTicker(ticker, rowIndex + 1),
    id: `${categoryKey}-${ticker.label.toLowerCase().replaceAll(" ", "-")}`,
  }));

  return {
    id: `${categoryKey}-${(group.label ?? group.tickers[0]?.label ?? `group-${column}`).toLowerCase().replaceAll(" ", "-")}`,
    label: group.label ?? group.tickers[0]?.label ?? `Group ${column}`,
    column,
    tickers,
  };
}

function createCategory(definition: ResourceCategoryDefinition): ResourceCategoryData {
  return {
    key: definition.key,
    label: definition.label,
    column: definition.column,
    icon: definition.icon,
    mixedItems: definition.mixedItems,
    tickerGroups: definition.groups.map((group, groupIndex) => createTickerGroup(definition.key, group, groupIndex + 1)),
  };
}

const categoryDefinitions: ResourceCategoryDefinition[] = [
  {
    key: "wood",
    label: "Wood",
    column: 1,
    icon: createElement(GiWoodStick),
    groups: [
      { tickers:  [
          resource("Oak Log", 2400, { tooltip: "Oak Log", icon: imageIcon(oakLogIconUrl) }),
          resource("Oak Plank", 1260, { tooltip: "Oak Plank: 360", icon: imageIcon(oakPlankIconUrl) }),
        ],
      },
      {
        label: "Generic Wood",
        tickers: [
          resource("Generic Log", 2400, { tooltip: "Generic Log", icon: imageIcon(pineLogIconUrl) }),
          resource("Generic Plank", 360, { tooltip: "Generic Plank: 360", icon: imageIcon(pinePlankIconUrl) }),
        ],
      },
      { tickers: [
          resource("Cedar Log", 999, { tooltip: "Cedar Log", icon: imageIcon(cedarLogIconUrl) }),
          resource("Cedar Plank", 0, { tooltip: "Cedar Plank: 0", icon: imageIcon(cedarPlankIconUrl) }),
      ],
    },
      { tickers: [
          resource("Ash Log", 9900, { tooltip: "Ash Log", icon: imageIcon(ashLogIconUrl) }),
          resource("Ash Plank", 0, { tooltip: "Ash Plank: 0", icon: imageIcon(ashPlankIconUrl) }),
      ],
     },
      { tickers: [
          resource("Ebony Log", 12, { tooltip: "Ebony Log", icon: imageIcon(ebonyLogIconUrl) }),
          resource("Ebony Plank", 1, { tooltip: "Ebony Plank: 1", icon: imageIcon(ebonyPlankIconUrl) }),
        ]
        },
        { tickers: [ resource("Bamboo", 500, { tooltip: "Bamboo", icon: imageIcon(bambooIconUrl) }), ],
      },
    ],
  },
  {
    key: "stone",
    label: "Stone",
    column: 2,
    icon: createElement(GiStoneBlock),
    groups: [
      { tickers: [resource("Limestone", 10_000)] },
      { tickers: [resource("Sandstone", 15_500)] },
      { tickers: [resource("Granite", 20_000)] },
      { tickers: [resource("Basalt", 45_000)] },
      { tickers: [resource("Generic Rock", 75_000)] },
      { tickers: [resource("Marble", 3_500, { isLuxury: true })] },
      { tickers: [resource("Flint", 1_200)] },
      { tickers: [resource("Obsidian", 420, { isLuxury: true })] },
      { tickers: [resource("Slate", 8_000)] },
    ],
  },
  {
    key: "other",
    label: "Other",
    column: 3,
    icon: createElement(GiClayBrick),
    mixedItems: true,
    groups: [
      { tickers: [resource("Bone", 500, { icon: imageIcon(boneIconUrl) })] },
      { tickers: [resource("Fodder", 42, { icon: imageIcon(grassIconUrl) })] },
      { tickers: [resource("Ochre", 650)] },
      { tickers: [resource("Chalk", 2_250)] },
    ],
  },
];

export const resourceDummyData: ResourceCategoryData[] = categoryDefinitions.map(createCategory);