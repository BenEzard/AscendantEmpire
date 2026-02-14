import type { ReactNode } from "react";
import ResourceCategory from "./ResourceCategory";
import "./ResourceTickerCollection.css";

export type ResourceTickerData = {
  id: string;
  label?: string;
  column: number;
  amount: number;
  isLuxury?: boolean;
  iconColor?: string;
  icon?: ReactNode;
};

export type ResourceCategoryData = {
  key: string;
  label: string;
  column: number;
  icon: ReactNode;
  tickers: ResourceTickerData[];
  mixedItems?: boolean;
};

export interface ResourceTickerCollectionProps {
  categories: ResourceCategoryData[];
  showTotals?: boolean;
  onClickAmount?: (args: { label: string; index: number; amount: number; isTotal: boolean }) => void;
  onReorderCategoryTickers?: (args: { categoryKey: string; tickers: ResourceTickerData[] }) => void;
}

export default function ResourceTickerCollection({
  categories,
  showTotals = true,
  onClickAmount,
  onReorderCategoryTickers,
}: ResourceTickerCollectionProps) {
  return (
    <div className="resource-ticker-collection">
      {categories.map((c) => (
        <ResourceCategory
          key={c.key}
          label={c.label}
          column={c.column}
          icon={c.icon}
          tickers={c.tickers}
          showTotal={showTotals}
          onClickAmount={onClickAmount}
          draggableTickers={!!onReorderCategoryTickers && c.mixedItems !== true}
          onReorderTickers={(tickers) => onReorderCategoryTickers?.({ categoryKey: c.key, tickers })}
        />
      ))}
    </div>
  );
}
