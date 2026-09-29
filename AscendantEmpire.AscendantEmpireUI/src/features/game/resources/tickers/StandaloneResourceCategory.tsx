import ResourceCategoryHeader from "./ResourceCategoryHeader";
import ResourceTicker from "./ResourceTicker";
import type { ResourceClickArgs, ResourceTickerData, ResourceCategoryType } from "./ResourceTickerCollection";
import { formatNumberWithCommas } from "@/util/number";
import "./ResourceTickerCollection.css";

export interface StandaloneResourceCategoryProps {
  categoryKey: string;
  categoryId?: number;
  categoryType: ResourceCategoryType;
  label: string;
  resources: ResourceTickerData[];
  maxColumns: number;
  onClickAmount?: (args: ResourceClickArgs) => void;
}

export default function StandaloneResourceCategory({
  categoryKey,
  categoryId,
  categoryType,
  label,
  resources,
  maxColumns,
  onClickAmount,
}: StandaloneResourceCategoryProps) {
  return (
    <div className="resource-category">
      <ResourceCategoryHeader label={label} />
      <div className="resource-standalone-category__grid" style={{ gridTemplateColumns: `repeat(${Math.max(1, maxColumns)}, max-content)` }}>
        {resources.map((resource, index) => (
          <div key={resource.code} className="resource-standalone-category__cell">
            <ResourceTicker
              name={resource.name}
              iconPath={resource.iconPath}
              amount={resource.amount}
              tooltip={resource.tooltip ?? `${resource.name}: ${formatNumberWithCommas(resource.amount)}`}
              IsLuxury={resource.isLuxury}
              isImported={resource.isImported}
              isExported={resource.isExported}
              hasStrategicStockpile={resource.hasStrategicStockpile}
              obsoleteIn={resource.obsoleteIn}
              demandNotMet={resource.demandNotMet}
              grade={resource.grade}
              onClick={
                onClickAmount
                  ? () =>
                      onClickAmount({
                        categoryCode: categoryKey,
                        categoryId,
                        categoryType,
                        name: resource.name,
                        amount: resource.amount,
                        isTotal: false,
                        index,
                        resourceCode: resource.code,
                        resourceId: resource.id,
                      })
                  : undefined
              }
            />
          </div>
        ))}
      </div>
    </div>
  );
}
