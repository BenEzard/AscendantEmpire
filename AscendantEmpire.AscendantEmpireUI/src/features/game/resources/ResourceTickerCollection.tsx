import type { ReactNode } from "react";
import ResourceCategory from "./ResourceCategory";
import "./ResourceTickerCollection.css";

export type ResourceTickerData = {
  id: string;
  name: string;
  column?: number;
  amount: number;
  tooltip?: string;
  isLuxury?: boolean;
  icon?: ReactNode;
};

export type ResourceTickerGroupData = {
  id: string;
  label?: string;
  column: number;
  tickers: ResourceTickerData[];
};

export type ResourceCategoryData = {
  key: string;
  label: string;
  column: number;
  icon: ReactNode;
  tickerGroups: ResourceTickerGroupData[];
  mixedItems?: boolean;
  showTotal?: boolean;
};

export interface ResourceTickerCollectionProps {
  categories: ResourceCategoryData[];
  showTotals?: boolean;
  onClickAmount?: (args: { name: string; index: number; amount: number; isTotal: boolean; rowIndex?: number }) => void;
  onReorderCategoryTickerGroups?: (args: { categoryKey: string; tickerGroups: ResourceTickerGroupData[] }) => void;
}

export default function ResourceTickerCollection({
  categories,
  showTotals = true,
  onClickAmount,
  onReorderCategoryTickerGroups,
}: ResourceTickerCollectionProps) {
  return (
    <div className="resource-ticker-collection">
      {categories.map((c) => (
        <ResourceCategory
          key={c.key}
          label={c.label}
          column={c.column}
          icon={c.icon}
          tickerGroups={c.tickerGroups}
          showTotal={showTotals && (c.showTotal ?? c.mixedItems !== true)}
          onClickAmount={onClickAmount}
          draggableTickerGroups={!!onReorderCategoryTickerGroups && c.mixedItems !== true}
          onReorderTickerGroups={(tickerGroups) => onReorderCategoryTickerGroups?.({ categoryKey: c.key, tickerGroups })}
        />
      ))}
    </div>
  );
}
