import ResourceCategoryHeader from "./ResourceCategoryHeader";
import ManufacturedResourceCollectionColumn from "./ManufacturedResourceCollectionColumn";
import bronzeShieldIconUrl from "@/assets/icons/bronze-shield_15px.png";
import silverShieldIconUrl from "@/assets/icons/silver-shield_15px.png";
import goldShieldIconUrl from "@/assets/icons/gold-shield_15px.png";
import type { ManufacturedGradesCategoryData, ResourceClickArgs } from "./ResourceTickerCollection";
import { MANUFACTURED_RESOURCE_GRADES } from "./ResourceConstants";
import "./ResourceTickerCollection.css";

const shieldIconByGrade = {
  bronze: bronzeShieldIconUrl,
  silver: silverShieldIconUrl,
  gold: goldShieldIconUrl,
} as const;

export interface ManufacturedGradesCategoryProps {
  category: ManufacturedGradesCategoryData;
  onClickAmount?: (args: ResourceClickArgs) => void;
}

export default function ManufacturedGradesCategory({ category, onClickAmount }: ManufacturedGradesCategoryProps) {
  return (
    <div className="resource-category" data-column={category.column}>
      <ResourceCategoryHeader label={category.label} />

      <div
        className="resource-manufactured__grid"
        style={{ gridTemplateColumns: `max-content repeat(${Math.max(1, category.collections.length)}, max-content)` }}>
        <div className="resource-manufactured__header-column" aria-hidden="true">
          <div className="resource-manufactured__header-top-spacer" />
          {MANUFACTURED_RESOURCE_GRADES.map((grade) => (
            <div key={grade} className="resource-manufactured__header-grade-row">
              <img src={shieldIconByGrade[grade]} alt={`${grade} quality`} className="resource-manufactured__shield-icon" />
            </div>
          ))}
        </div>

        {category.collections.map((collection) => (
          <ManufacturedResourceCollectionColumn
            key={collection.id}
            categoryKey={category.key}
            categoryType={category.type}
            collection={collection}
            onClickAmount={onClickAmount}
          />
        ))}
      </div>
    </div>
  );
}
