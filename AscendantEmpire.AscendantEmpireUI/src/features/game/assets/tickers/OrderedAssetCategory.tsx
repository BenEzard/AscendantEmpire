import { useMemo } from "react";
import OrderedAssetTickerGroup from "./OrderedAssetTickerGroup";
import AssetCategoryHeader from "./AssetCategoryHeader";
import "./AssetTickerCollection.css";
import type { AssetClickArgs, AssetTickerData, AssetTickerGroupData } from "./AssetTickerCollection";

export interface OrderedAssetCategoryProps {
  categoryKey: string;
  categoryId?: number;
  categoryType: "grouped-assets";
  label: string;
  tickerGroups: AssetTickerGroupData[];
  maxColumns: number;
  onClickAmount?: (args: AssetClickArgs) => void;
  draggableTickers?: boolean;
  onReorderGroupTickers?: (groupCode: string, tickers: AssetTickerData[]) => void;
}

function sortTickerGroupsByColumn(tickerGroups: AssetTickerGroupData[]): AssetTickerGroupData[] {
  return tickerGroups.slice().sort((a, b) => {
    const ac = Number.isFinite(a.column) ? a.column : Number.POSITIVE_INFINITY;
    const bc = Number.isFinite(b.column) ? b.column : Number.POSITIVE_INFINITY;
    return ac - bc;
  });
}

export default function OrderedAssetCategory({
  categoryKey,
  categoryId,
  categoryType,
  label,
  tickerGroups,
  maxColumns,
  onClickAmount,
  draggableTickers = false,
  onReorderGroupTickers,
}: OrderedAssetCategoryProps) {
  const orderedTickerGroups = useMemo(() => sortTickerGroupsByColumn(tickerGroups), [tickerGroups]);
  const maxTickerGroupColumn = useMemo(() => tickerGroups.reduce((acc, group) => Math.max(acc, group.column), 0), [tickerGroups]);
  const resolvedMaxColumns = Math.max(maxColumns, maxTickerGroupColumn);
  const gridTemplateColumns = useMemo(() => {
    if (resolvedMaxColumns > 0) return `repeat(${resolvedMaxColumns}, max-content)`;
    return undefined;
  }, [resolvedMaxColumns]);

  return (
    <div className="asset-category">
      <AssetCategoryHeader label={label} />
      <div className="asset-category__tickers" style={gridTemplateColumns ? { gridTemplateColumns } : undefined}>
        {orderedTickerGroups.map((group) => (
          <div key={group.code} data-ticker-group-column={group.column} className="asset-category__cell" style={{ gridColumnStart: group.column }}>
            <OrderedAssetTickerGroup
              label={group.label ?? label}
              tickers={group.tickers}
              draggableTickers={draggableTickers}
              onReorderTickers={onReorderGroupTickers ? (tickers) => onReorderGroupTickers(group.code, tickers) : undefined}
              onClickTicker={
                onClickAmount
                  ? (ticker) =>
                      onClickAmount({
                        categoryCode: categoryKey,
                        categoryId,
                        categoryType,
                        name: ticker.name,
                        index: group.column,
                        amount: ticker.amount,
                        isTotal: false,
                        rowIndex: group.tickers.findIndex((item) => item.code === ticker.code),
                        groupCode: group.code,
                        groupId: group.id,
                        assetCode: ticker.code,
                        assetId: ticker.id,
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
