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
  column: number;
  resources: ResourceTickerData[];
  maxColumns: number;
  onClickAmount?: (args: ResourceClickArgs) => void;
}

export default function StandaloneResourceCategory({
  categoryKey,
  categoryId,
  categoryType,
  label,
  column,
  resources,
  maxColumns,
  onClickAmount,
}: StandaloneResourceCategoryProps) {
  if (resources.every((r) => r.amount === 0)) return null;

  return (
    <div className="resource-category" data-column={column}>
      <ResourceCategoryHeader label={label} />
      <div className="resource-standalone-category__grid" style={{ gridTemplateColumns: `repeat(${Math.max(1, maxColumns)}, max-content)` }}>
        {resources.map((resource, index) => (
          <div
            key={resource.code}
            className={`resource-standalone-category__cell${resource.amount === 0 ? " resource-standalone-category__cell--zero" : ""}`}>
            <ResourceTicker
              name={resource.name}
              icon={resource.icon}
              amount={resource.amount}
              tooltip={resource.tooltip ?? `${resource.name}: ${formatNumberWithCommas(resource.amount)}`}
              IsLuxury={resource.isLuxury === true}
              isImported={resource.isImported}
              isExported={resource.isExported}
              hasStrategicStockpile={resource.hasStrategicStockpile}
              soonToBeObsolete={resource.soonToBeObsolete}
              demandNotMet={resource.demandNotMet}
              grade={resource.quality}
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
