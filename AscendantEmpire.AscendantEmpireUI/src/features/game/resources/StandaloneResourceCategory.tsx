import ResourceCategoryHeader from "./ResourceCategoryHeader";
import ResourceTicker from "./ResourceTicker";
import type { ResourceClickArgs, ResourceTickerData, ResourceCategoryType } from "./ResourceTickerCollection";
import { formatNumberWithCommas } from "@/util/number";
import "./ResourceTickerCollection.css";

export interface StandaloneResourceCategoryProps {
  categoryKey: string;
  categoryType: ResourceCategoryType;
  label: string;
  column: number;
  resources: ResourceTickerData[];
  maxColumns: number;
  onClickAmount?: (args: ResourceClickArgs) => void;
}

export default function StandaloneResourceCategory({
  categoryKey,
  categoryType,
  label,
  column,
  resources,
  maxColumns,
  onClickAmount,
}: StandaloneResourceCategoryProps) {
  return (
    <div className="resource-category" data-column={column}>
      <ResourceCategoryHeader label={label} />
      <div className="resource-standalone-category__grid" style={{ gridTemplateColumns: `repeat(${Math.max(1, maxColumns)}, max-content)` }}>
        {resources.map((resource, index) => (
          <div key={resource.id} className="resource-standalone-category__cell">
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
              onClick={
                onClickAmount
                  ? () =>
                      onClickAmount({
                        categoryKey,
                        categoryType,
                        name: resource.name,
                        amount: resource.amount,
                        isTotal: false,
                        index,
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
