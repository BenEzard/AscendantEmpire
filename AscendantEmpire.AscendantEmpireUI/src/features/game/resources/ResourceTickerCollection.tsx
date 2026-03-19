import type { ReactNode } from "react";
import ProcessedResourceCategory from "./ProcessedResourceCategory";
import StandaloneResourceCategory from "./StandaloneResourceCategory";
import ManufacturedGradesCategory from "./ManufacturedGradesCategory";
import "./ResourceTickerCollection.css";
import type { ManufacturedResourceGrade } from "./ResourceConstants";

export type ResourceTickerData = {
  id: string;
  name: string;
  column?: number;
  amount: number;
  tooltip?: string;
  isLuxury?: boolean;
  icon?: ReactNode;
  isImported?: boolean;
  isExported?: boolean;
  hasStrategicStockpile?: boolean;
  soonToBeObsolete?: boolean;
  demandNotMet?: boolean;
  grade?: ManufacturedResourceGrade;
};

export type ResourceTickerGroupData = {
  id: string;
  label?: string;
  column: number;
  tickers: ResourceTickerData[];
};

export type ResourceCategoryType = "processed-resources" | "standalone-resources" | "manufactured-grades";

export type ResourceClickArgs = {
  categoryKey: string;
  categoryType: ResourceCategoryType;
  name: string;
  amount: number;
  isTotal: boolean;
  index?: number;
  rowIndex?: number;
  groupId?: string;
  resourceId?: string;
  collectionId?: string;
  grade?: ManufacturedResourceGrade;
};

export type ProcessedResourceCategoryData = {
  key: string;
  type: "processed-resources";
  label: string;
  column: number;
  icon: ReactNode;
  tickerGroups: ResourceTickerGroupData[];
  showTotal?: boolean;
  maxColumns?: number;
};

export type StandaloneResourceCategoryData = {
  key: string;
  type: "standalone-resources";
  label: string;
  column: number;
  icon: ReactNode;
  resources: ResourceTickerData[];
};

export type ManufacturedResourceCollectionData = {
  id: string;
  label: string;
  icon: ReactNode;
  resourcesByGrade: Record<ManufacturedResourceGrade, ResourceTickerData>;
};

export type ManufacturedGradesCategoryData = {
  key: string;
  type: "manufactured-grades";
  label: string;
  column: number;
  icon: ReactNode;
  collections: ManufacturedResourceCollectionData[];
};

export type ResourceCategoryData = ProcessedResourceCategoryData | StandaloneResourceCategoryData | ManufacturedGradesCategoryData;

export interface ResourceTickerCollectionProps {
  categories: ResourceCategoryData[];
  showTotals?: boolean;
  maximumColumns?: number;
  onClickAmount?: (args: ResourceClickArgs) => void;
  onReorderCategoryTickerGroups?: (args: { categoryKey: string; tickerGroups: ResourceTickerGroupData[] }) => void;
}

function getProcessedCategoryColumnCount(category: ProcessedResourceCategoryData): number {
  const actualMax = category.tickerGroups.reduce((max, group) => Math.max(max, group.column), 0);
  return Math.max(category.maxColumns ?? 0, actualMax);
}

export default function ResourceTickerCollection({
  categories,
  showTotals = true,
  maximumColumns = 0,
  onClickAmount,
  onReorderCategoryTickerGroups,
}: ResourceTickerCollectionProps) {
  const processedCategoryColumnCount = categories.reduce(
    (max, category) => (category.type === "processed-resources" ? Math.max(max, getProcessedCategoryColumnCount(category)) : max),
    0,
  );
  const resolvedMaxColumns = Math.max(maximumColumns, processedCategoryColumnCount);

  return (
    <div className="resource-ticker-collection">
      {categories.map((category) => {
        switch (category.type) {
          case "processed-resources":
            return (
              <ProcessedResourceCategory
                key={category.key}
                categoryKey={category.key}
                categoryType={category.type}
                label={category.label}
                column={category.column}
                tickerGroups={category.tickerGroups}
                maxColumns={resolvedMaxColumns}
                showTotal={showTotals && (category.showTotal ?? true)}
                onClickAmount={onClickAmount}
                draggableTickerGroups={!!onReorderCategoryTickerGroups}
                onReorderTickerGroups={(tickerGroups) => onReorderCategoryTickerGroups?.({ categoryKey: category.key, tickerGroups })}
              />
            );

          case "standalone-resources":
            return (
              <StandaloneResourceCategory
                key={category.key}
                categoryKey={category.key}
                categoryType={category.type}
                label={category.label}
                column={category.column}
                resources={category.resources}
                maxColumns={resolvedMaxColumns}
                onClickAmount={onClickAmount}
              />
            );

          case "manufactured-grades":
            return <ManufacturedGradesCategory key={category.key} category={category} onClickAmount={onClickAmount} />;

          default:
            return null;
        }
      })}
    </div>
  );
}
