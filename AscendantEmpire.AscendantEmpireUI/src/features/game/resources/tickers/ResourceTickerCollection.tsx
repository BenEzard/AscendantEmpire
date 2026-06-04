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
  column?: number;
  amount: number;
  resourcesAddedPerTurn?: number;
  description?: string;
  tooltip?: string;
  isLuxury?: boolean;
  icon?: ReactNode;
  isImported?: boolean;
  isExported?: boolean;
  hasStrategicStockpile?: boolean;
  // Number of turns until this resource becomes obsolete; omit or 0 means not obsoleting
  obsoleteIn?: number;
  demandNotMet?: boolean;
  quality?: ResourceGrade;
  distributionConfig?: DistributionConfig;
  productionConfig?: ProductionConfig;
};

export type ResourceTickerGroupData = {
  id: number;
  code: string;
  label?: string;
  column: number;
  tickers: ResourceTickerData[];
  distributionConfig?: DistributionConfig;
};

export type ResourceCategoryType = "processed-resources" | "standalone-resources";

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

export type ProcessedResourceCategoryData = {
  id: number;
  code: string;
  type: "processed-resources";
  label: string;
  column: number;
  icon: ReactNode;
  tickerGroups: ResourceTickerGroupData[];
  maxColumns?: number;
};

export type StandaloneResourceCategoryData = {
  id: number;
  code: string;
  type: "standalone-resources";
  label: string;
  column: number;
  icon: ReactNode;
  resources: ResourceTickerData[];
};

export type ResourceCategoryData = ProcessedResourceCategoryData | StandaloneResourceCategoryData;

export interface ResourceTickerCollectionProps {
  categories: ResourceCategoryData[];
  showTotals?: boolean;
  maxColumns?: number;
  onClickAmount?: (args: ResourceClickArgs) => void;
  onReorderGroupTickers?: (args: { categoryCode: string; groupCode: string; tickers: ResourceTickerData[] }) => void;
}

function getProcessedCategoryColumnCount(category: ProcessedResourceCategoryData): number {
  const actualMax = category.tickerGroups.reduce((max, group) => Math.max(max, group.column), 0);
  return Math.max(category.maxColumns ?? 0, actualMax);
}

export default function ResourceTickerCollection({
  categories,
  showTotals = true,
  maxColumns = 0,
  onClickAmount,
  onReorderGroupTickers,
}: ResourceTickerCollectionProps) {
  const processedCategoryColumnCount = categories.reduce(
    (max, category) => (category.type === "processed-resources" ? Math.max(max, getProcessedCategoryColumnCount(category)) : max),
    0,
  );
  const resolvedProcessedColumns = Math.max(maxColumns, processedCategoryColumnCount);

  const maxTickersInAnyGroup = categories.reduce(
    (max, category) =>
      category.type === "processed-resources"
        ? Math.max(
            max,
            category.tickerGroups.reduce((m, g) => Math.max(m, g.tickers.filter((t) => t.amount !== 0).length), 0),
          )
        : max,
    0,
  );
  const resolvedStandaloneColumns = Math.max(maxColumns, maxTickersInAnyGroup);

  return (
    <div className="resource-ticker-collection">
      {categories.map((category) => {
        switch (category.type) {
          case "processed-resources":
            return (
              <OrderedResourceCategory
                key={category.code}
                categoryKey={category.code}
                categoryId={category.id}
                categoryType={category.type}
                label={category.label}
                column={category.column}
                tickerGroups={category.tickerGroups}
                maxColumns={resolvedProcessedColumns}
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
                column={category.column}
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
