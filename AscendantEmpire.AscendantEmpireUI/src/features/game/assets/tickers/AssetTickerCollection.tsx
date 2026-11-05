import type { ReactNode } from "react";
import OrderedAssetCategory from "./OrderedAssetCategory";
import StandaloneAssetCategory from "./StandaloneAssetCategory";
import "./AssetTickerCollection.css";
import type { AssetGrade } from "./AssetConstants";
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

export type AssetTickerData = {
  id: number;
  code: string;
  name: string;
  /** Root-relative URL of the asset's catalogue icon, or "" where it has none. */
  iconPath: string;
  amount: number;
  amountAddedPerTurn: number;
  description?: string;
  tooltip?: string;
  isLuxury: boolean;
  isImported: boolean;
  isExported: boolean;
  hasStrategicReserve: boolean;
  // Number of rounds until this asset becomes obsolete; omit or 0 means not obsoleting
  obsoleteIn?: number;
  demandNotMet: boolean;
  grade: AssetGrade;
  // No wire source yet — the asset manager's sliders and charts fall back to their own empty states without one.
  distributionConfig?: DistributionConfig;
  productionConfig?: ProductionConfig;
};

export type AssetTickerGroupData = {
  id: number;
  code: string;
  label?: string;
  /** The player whose people this group counts, when it was projected from a per-other-player template; null for an ordinary group. */
  subjectPlayerId?: number | null;
  /** 1-based grid column this group occupies, taken from its position in the order the API returned. */
  column: number;
  tickers: AssetTickerData[];
  distributionConfig?: DistributionConfig;
};

/** Mapped from the API's `AssetCategoryType` member names — `GroupedAssets` and `StandaloneAssets`. */
export type AssetCategoryType = "grouped-assets" | "standalone-assets";

export type AssetClickArgs = {
  categoryCode: string;
  categoryId?: number;
  categoryType: AssetCategoryType;
  name: string;
  amount: number;
  isTotal: boolean;
  index?: number;
  rowIndex?: number;
  groupCode?: string;
  groupId?: number;
  assetCode?: string;
  assetId?: number;
  collectionId?: string;
};

export type GroupedAssetCategoryData = {
  id: number;
  code: string;
  type: "grouped-assets";
  label: string;
  tickerGroups: AssetTickerGroupData[];
  maxColumns?: number;
};

export type StandaloneAssetCategoryData = {
  id: number;
  code: string;
  type: "standalone-assets";
  label: string;
  assets: AssetTickerData[];
};

export type AssetCategoryData = GroupedAssetCategoryData | StandaloneAssetCategoryData;

export interface AssetTickerCollectionProps {
  categories: AssetCategoryData[];
  maxColumns?: number;
  onClickAmount?: (args: AssetClickArgs) => void;
  onReorderGroupTickers?: (args: { categoryCode: string; groupCode: string; tickers: AssetTickerData[] }) => void;
}

function getGroupedCategoryColumnCount(category: GroupedAssetCategoryData): number {
  const actualMax = category.tickerGroups.reduce((max, group) => Math.max(max, group.column), 0);
  return Math.max(category.maxColumns ?? 0, actualMax);
}

export default function AssetTickerCollection({
  categories,
  maxColumns = 0,
  onClickAmount,
  onReorderGroupTickers,
}: AssetTickerCollectionProps) {
  // Grouped categories lay their groups out in real grid columns, so the collection is as wide as its widest grouped category.
  const groupedCategoryColumnCount = categories.reduce(
    (max, category) => (category.type === "grouped-assets" ? Math.max(max, getGroupedCategoryColumnCount(category)) : max),
    0,
  );
  const resolvedGroupedColumns = Math.max(maxColumns, groupedCategoryColumnCount);

  // A standalone grid sits below the grouped rows in the same panel, so it is made at least as wide as the widest group row and the panel's right
  // edge lines up instead of stepping. Standalone categories deliberately contribute nothing here — a panel with no grouped category falls back to
  // the caller's floor.
  const maxTickersInAnyGroup = categories.reduce(
    (max, category) =>
      category.type === "grouped-assets"
        ? Math.max(
            max,
            category.tickerGroups.reduce((groupMax, group) => Math.max(groupMax, group.tickers.length), 0),
          )
        : max,
    0,
  );
  const resolvedStandaloneColumns = Math.max(maxColumns, maxTickersInAnyGroup);

  return (
    <div className="asset-ticker-collection">
      {categories.map((category) => {
        switch (category.type) {
          case "grouped-assets":
            return (
              <OrderedAssetCategory
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

          case "standalone-assets":
            return (
              <StandaloneAssetCategory
                key={category.code}
                categoryKey={category.code}
                categoryId={category.id}
                categoryType={category.type}
                label={category.label}
                assets={category.assets}
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
