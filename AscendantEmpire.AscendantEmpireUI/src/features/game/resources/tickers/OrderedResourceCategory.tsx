import { useMemo } from "react";
import OrderedResourceTickerGroup from "./OrderedResourceTickerGroup";
import ResourceCategoryHeader from "./ResourceCategoryHeader";
import "./ResourceTickerCollection.css";
import type { ResourceClickArgs, ResourceTickerData, ResourceTickerGroupData } from "./ResourceTickerCollection";

export interface OrderedResourceCategoryProps {
  categoryKey: string;
  categoryId?: number;
  categoryType: "processed-resources";
  label: string;
  column: number;
  tickerGroups: ResourceTickerGroupData[];
  maxColumns: number;
  onClickAmount?: (args: ResourceClickArgs) => void;
  draggableTickers?: boolean;
  onReorderGroupTickers?: (groupCode: string, tickers: ResourceTickerData[]) => void;
}

function sortTickerGroupsByColumn(tickerGroups: ResourceTickerGroupData[]): ResourceTickerGroupData[] {
  return tickerGroups.slice().sort((a, b) => {
    const ac = Number.isFinite(a.column) ? a.column : Number.POSITIVE_INFINITY;
    const bc = Number.isFinite(b.column) ? b.column : Number.POSITIVE_INFINITY;
    return ac - bc;
  });
}

export default function OrderedResourceCategory({
  categoryKey,
  categoryId,
  categoryType,
  label,
  column,
  tickerGroups,
  maxColumns,
  onClickAmount,
  draggableTickers = false,
  onReorderGroupTickers,
}: OrderedResourceCategoryProps) {
  const orderedTickerGroups = useMemo(() => sortTickerGroupsByColumn(tickerGroups), [tickerGroups]);
  const maxTickerGroupColumn = useMemo(() => tickerGroups.reduce((acc, group) => Math.max(acc, group.column), 0), [tickerGroups]);
  const resolvedMaxColumns = Math.max(maxColumns, maxTickerGroupColumn);
  const gridTemplateColumns = useMemo(() => {
    if (resolvedMaxColumns > 0) return `repeat(${resolvedMaxColumns}, max-content)`;
    return undefined;
  }, [resolvedMaxColumns]);

  if (tickerGroups.every((g) => g.tickers.every((t) => t.amount === 0))) return null;

  return (
    <div className="resource-category" data-column={column}>
      <ResourceCategoryHeader label={label} />
      <div className="resource-category__tickers" style={gridTemplateColumns ? { gridTemplateColumns } : undefined}>
        {orderedTickerGroups.map((group) => (
          <div key={group.code} data-ticker-group-column={group.column} className="resource-category__cell" style={{ gridColumnStart: group.column }}>
            <OrderedResourceTickerGroup
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
                        resourceCode: ticker.code,
                        resourceId: ticker.id,
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
