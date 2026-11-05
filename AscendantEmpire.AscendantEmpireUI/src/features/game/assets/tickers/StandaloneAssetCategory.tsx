import AssetCategoryHeader from "./AssetCategoryHeader";
import AssetTicker from "./AssetTicker";
import type { AssetClickArgs, AssetTickerData, AssetCategoryType } from "./AssetTickerCollection";
import { formatNumberWithCommas } from "@/util/number";
import "./AssetTickerCollection.css";

export interface StandaloneAssetCategoryProps {
  categoryKey: string;
  categoryId?: number;
  categoryType: AssetCategoryType;
  label: string;
  assets: AssetTickerData[];
  maxColumns: number;
  onClickAmount?: (args: AssetClickArgs) => void;
}

export default function StandaloneAssetCategory({
  categoryKey,
  categoryId,
  categoryType,
  label,
  assets,
  maxColumns,
  onClickAmount,
}: StandaloneAssetCategoryProps) {
  return (
    <div className="asset-category">
      <AssetCategoryHeader label={label} />
      <div className="asset-standalone-category__grid" style={{ gridTemplateColumns: `repeat(${Math.max(1, maxColumns)}, max-content)` }}>
        {assets.map((asset, index) => (
          <div key={asset.code} className="asset-standalone-category__cell">
            <AssetTicker
              name={asset.name}
              iconPath={asset.iconPath}
              amount={asset.amount}
              tooltip={asset.tooltip ?? `${asset.name}: ${formatNumberWithCommas(asset.amount)}`}
              IsLuxury={asset.isLuxury}
              isImported={asset.isImported}
              isExported={asset.isExported}
              hasStrategicReserve={asset.hasStrategicReserve}
              obsoleteIn={asset.obsoleteIn}
              demandNotMet={asset.demandNotMet}
              grade={asset.grade}
              onClick={
                onClickAmount
                  ? () =>
                      onClickAmount({
                        categoryCode: categoryKey,
                        categoryId,
                        categoryType,
                        name: asset.name,
                        amount: asset.amount,
                        isTotal: false,
                        index,
                        assetCode: asset.code,
                        assetId: asset.id,
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
