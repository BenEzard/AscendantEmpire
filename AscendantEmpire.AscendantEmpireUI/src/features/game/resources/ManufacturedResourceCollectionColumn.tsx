import ResourceTicker from "./ResourceTicker";
import ResourceTotal from "./ResourceTotal";
import type { ManufacturedResourceCollectionData, ResourceClickArgs, ResourceCategoryType } from "./ResourceTickerCollection";
import { MANUFACTURED_RESOURCE_GRADES, type ManufacturedResourceGrade } from "./ResourceConstants";
import { formatNumberWithCommas } from "@/util/number";

export interface ManufacturedResourceCollectionColumnProps {
  categoryKey: string;
  categoryType: ResourceCategoryType;
  collection: ManufacturedResourceCollectionData;
  onClickAmount?: (args: ResourceClickArgs) => void;
}

function getAggregateFlags(collection: ManufacturedResourceCollectionData) {
  return MANUFACTURED_RESOURCE_GRADES.reduce(
    (acc, grade) => {
      const resource = collection.resourcesByGrade[grade];
      return {
        isImported: acc.isImported || resource.isImported === true,
        isExported: acc.isExported || resource.isExported === true,
        hasStrategicStockpile: acc.hasStrategicStockpile || resource.hasStrategicStockpile === true,
        soonToBeObsolete: acc.soonToBeObsolete || resource.soonToBeObsolete === true,
        demandNotMet: acc.demandNotMet || resource.demandNotMet === true,
        isLuxury: acc.isLuxury || resource.isLuxury === true,
      };
    },
    {
      isImported: false,
      isExported: false,
      hasStrategicStockpile: false,
      soonToBeObsolete: false,
      demandNotMet: false,
      isLuxury: false,
    },
  );
}

function getCollectionTotal(collection: ManufacturedResourceCollectionData): number {
  return MANUFACTURED_RESOURCE_GRADES.reduce((sum, grade) => sum + collection.resourcesByGrade[grade].amount, 0);
}

function getGradeLabel(label: string, grade: ManufacturedResourceGrade) {
  return `${label}_${grade[0].toUpperCase()}${grade.slice(1)}`;
}

export default function ManufacturedResourceCollectionColumn({
  categoryKey,
  categoryType,
  collection,
  onClickAmount,
}: ManufacturedResourceCollectionColumnProps) {
  const aggregate = getAggregateFlags(collection);
  const total = getCollectionTotal(collection);

  return (
    <div className="resource-manufactured__column">
      <div className="resource-manufactured__top-cell">
        <ResourceTicker
          name={`${collection.label} total`}
          icon={collection.icon}
          amount={total}
          tooltip={`${collection.label} total: ${formatNumberWithCommas(total)}`}
          IsLuxury={aggregate.isLuxury}
          isImported={aggregate.isImported}
          isExported={aggregate.isExported}
          hasStrategicStockpile={aggregate.hasStrategicStockpile}
          soonToBeObsolete={aggregate.soonToBeObsolete}
          demandNotMet={aggregate.demandNotMet}
          onClick={
            onClickAmount
              ? () =>
                  onClickAmount({
                    categoryKey,
                    categoryType,
                    name: `${collection.label} total`,
                    amount: total,
                    isTotal: true,
                    collectionId: collection.id,
                  })
              : undefined
          }
        />
      </div>

      {MANUFACTURED_RESOURCE_GRADES.map((grade) => {
        const resource = collection.resourcesByGrade[grade];
        return (
          <div key={grade} className="resource-manufactured__grade-row">
            <ResourceTotal
              amount={resource.amount}
              variant="compact"
              tooltip={resource.tooltip ?? `${getGradeLabel(collection.label, grade)}: ${formatNumberWithCommas(resource.amount)}`}
              onClick={
                onClickAmount
                  ? () =>
                      onClickAmount({
                        categoryKey,
                        categoryType,
                        name: resource.name,
                        amount: resource.amount,
                        isTotal: false,
                        collectionId: collection.id,
                        resourceId: resource.id,
                        grade,
                      })
                  : undefined
              }
            />
          </div>
        );
      })}
    </div>
  );
}
