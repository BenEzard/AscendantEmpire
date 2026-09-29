import type { ReactNode } from "react";
import OrderedResourceCategory from "./OrderedResourceCategory";
import StandaloneResourceCategory from "./StandaloneResourceCategory";
import "./ResourceTickerCollection.css";
import type { ResourceGrade } from "./ResourceConstants";
import type { LockedState } from "../../../../components/ui/slider/Slider";

// -- Distribution configuration types --

export type DistributionSliderDef = {
  id: number;
  code: string;
  label: string;
  icon?: ReactNode;
  initialValue: number;
  lockedState?: LockedState;
  canConsumeRemainder?: boolean;
  willConsumeRemainder?: boolean;
  maxConsumptionLimit?: number;
};

export type DistributionSliderGroupDef = {
  sliders: DistributionSliderDef[];
};

export type DistributionConfig = {
  sliderGroups: DistributionSliderGroupDef[];
};

export type RegionProductionData = {
  name: string;
  thisTurn: number;
  lastTurn: number;
};

export type ProductionHistoryEntry = {
  turn: number;
  production: number;
  demand: number;
};

export type ProductionConfig = {
  topRegions: RegionProductionData[];
  history: ProductionHistoryEntry[];
};

export type ResourceTickerData = {
  id: number;
  code: string;
  name: string;
  /** Root-relative URL of the resource's catalogue icon, or "" where it has none. */
  iconPath: string;
  amount: number;
  amountAddedPerTurn: number;
  description?: string;
  tooltip?: string;
  isLuxury: boolean;
  isImported: boolean;
  isExported: boolean;
  hasStrategicStockpile: boolean;
  // Number of rounds until this resource becomes obsolete; omit or 0 means not obsoleting
  obsoleteIn?: number;
  demandNotMet: boolean;
  grade: ResourceGrade;
  // No wire source yet — the resource manager's sliders and charts fall back to their own empty states without one.
  distributionConfig?: DistributionConfig;
  productionConfig?: ProductionConfig;
};

export type ResourceTickerGroupData = {
  id: number;
  code: string;
  label?: string;
  /** 1-based grid column this group occupies, taken from its position in the order the API returned. */
  column: number;
  tickers: ResourceTickerData[];
  distributionConfig?: DistributionConfig;
};

/** Mapped from the API's `ResourceCategoryType` member names — `GroupedResources` and `StandaloneResources`. */
export type ResourceCategoryType = "grouped-resources" | "standalone-resources";

export type ResourceClickArgs = {
  categoryCode: string;
  categoryId?: number;
  categoryType: ResourceCategoryType;
  name: string;
  amount: number;
  isTotal: boolean;
  index?: number;
  rowIndex?: number;
  groupCode?: string;
  groupId?: number;
  resourceCode?: string;
  resourceId?: number;
  collectionId?: string;
};

export type GroupedResourceCategoryData = {
  id: number;
  code: string;
  type: "grouped-resources";
  label: string;
  tickerGroups: ResourceTickerGroupData[];
  maxColumns?: number;
};

export type StandaloneResourceCategoryData = {
  id: number;
  code: string;
  type: "standalone-resources";
  label: string;
  resources: ResourceTickerData[];
};

export type ResourceCategoryData = GroupedResourceCategoryData | StandaloneResourceCategoryData;

export interface ResourceTickerCollectionProps {
  categories: ResourceCategoryData[];
  maxColumns?: number;
  onClickAmount?: (args: ResourceClickArgs) => void;
  onReorderGroupTickers?: (args: { categoryCode: string; groupCode: string; tickers: ResourceTickerData[] }) => void;
}

function getGroupedCategoryColumnCount(category: GroupedResourceCategoryData): number {
  const actualMax = category.tickerGroups.reduce((max, group) => Math.max(max, group.column), 0);
  return Math.max(category.maxColumns ?? 0, actualMax);
}

export default function ResourceTickerCollection({
  categories,
  maxColumns = 0,
  onClickAmount,
  onReorderGroupTickers,
}: ResourceTickerCollectionProps) {
  // Grouped categories lay their groups out in real grid columns, so the collection is as wide as its widest grouped category.
  const groupedCategoryColumnCount = categories.reduce(
    (max, category) => (category.type === "grouped-resources" ? Math.max(max, getGroupedCategoryColumnCount(category)) : max),
    0,
  );
  const resolvedGroupedColumns = Math.max(maxColumns, groupedCategoryColumnCount);

  // A standalone grid sits below the grouped rows in the same panel, so it is made at least as wide as the widest group row and the panel's right
  // edge lines up instead of stepping. Standalone categories deliberately contribute nothing here — a panel with no grouped category falls back to
  // the caller's floor.
  const maxTickersInAnyGroup = categories.reduce(
    (max, category) =>
      category.type === "grouped-resources"
        ? Math.max(
            max,
            category.tickerGroups.reduce((groupMax, group) => Math.max(groupMax, group.tickers.length), 0),
          )
        : max,
    0,
  );
  const resolvedStandaloneColumns = Math.max(maxColumns, maxTickersInAnyGroup);

  return (
    <div className="resource-ticker-collection">
      {categories.map((category) => {
        switch (category.type) {
          case "grouped-resources":
            return (
              <OrderedResourceCategory
                key={category.code}
                categoryKey={category.code}
                categoryId={category.id}
                categoryType={category.type}
                label={category.label}
                tickerGroups={category.tickerGroups}
                maxColumns={resolvedGroupedColumns}
                onClickAmount={onClickAmount}
                draggableTickers={!!onReorderGroupTickers}
                onReorderGroupTickers={
                  onReorderGroupTickers
                    ? (groupCode, tickers) => onReorderGroupTickers({ categoryCode: category.code, groupCode, tickers })
                    : undefined
                }
              />
            );

          case "standalone-resources":
            return (
              <StandaloneResourceCategory
                key={category.code}
                categoryKey={category.code}
                categoryId={category.id}
                categoryType={category.type}
                label={category.label}
                resources={category.resources}
                maxColumns={resolvedStandaloneColumns}
                onClickAmount={onClickAmount}
              />
            );

          default:
            return null;
        }
      })}
    </div>
  );
}
