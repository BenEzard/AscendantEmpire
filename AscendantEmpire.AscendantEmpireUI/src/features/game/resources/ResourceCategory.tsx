import { useMemo, useRef, useState, type ReactNode } from "react";
import ResourceTickerGroup from "./ResourceTickerGroup";
import ResourceTotal from "./ResourceTotal";
import ResourceCategoryHeader from "./ResourceCategoryHeader";
import "./ResourceTickerCollection.css";
import type { ResourceTickerGroupData } from "./ResourceTickerCollection";
import { formatNumberWithCommas } from "@/util/number";

export interface ResourceCategoryProps {
  label: string;
  column: number;
  icon: ReactNode;
  tickerGroups: ResourceTickerGroupData[];
  showTotal?: boolean;
  onClickAmount?: (args: { name: string; index: number; amount: number; isTotal: boolean; rowIndex?: number }) => void;
  draggableTickerGroups?: boolean;
  onReorderTickerGroups?: (tickerGroups: ResourceTickerGroupData[]) => void;
}

function sortTickerGroupsByColumn(tickerGroups: ResourceTickerGroupData[]): ResourceTickerGroupData[] {
  return tickerGroups.slice().sort((a, b) => {
    const ac = Number.isFinite(a.column) ? a.column : Number.POSITIVE_INFINITY;
    const bc = Number.isFinite(b.column) ? b.column : Number.POSITIVE_INFINITY;
    return ac - bc;
  });
}

function reorderTickerGroupByColumn(tickerGroups: ResourceTickerGroupData[], draggedId: string, targetColumn: number): ResourceTickerGroupData[] {
  const ordered = sortTickerGroupsByColumn(tickerGroups);
  const fromIndex = ordered.findIndex((group) => group.id === draggedId);
  if (fromIndex < 0) return ordered;

  const toIndex = Math.max(0, Math.min(ordered.length - 1, targetColumn - 1));
  if (fromIndex === toIndex) return ordered;

  const next = ordered.slice();
  const [moved] = next.splice(fromIndex, 1);
  next.splice(toIndex, 0, moved);

  return next.map((group, idx) => ({ ...group, column: idx + 1 }));
}

export default function ResourceCategory({
  label,
  column,
  icon: _icon,
  tickerGroups,
  showTotal = true,
  onClickAmount,
  draggableTickerGroups = false,
  onReorderTickerGroups,
}: ResourceCategoryProps) {
  const orderedTickerGroups = useMemo(() => sortTickerGroupsByColumn(tickerGroups), [tickerGroups]);
  const maxTickerDepth = useMemo(
    () => orderedTickerGroups.reduce((maxDepth, group) => Math.max(maxDepth, group.tickers.length), 0),
    [orderedTickerGroups],
  );
  const rowTotals = useMemo(
    () =>
      Array.from({ length: maxTickerDepth }, (_, rowIndex) =>
        orderedTickerGroups.reduce((rowTotal, group) => rowTotal + (group.tickers[rowIndex]?.amount ?? 0), 0),
      ),
    [maxTickerDepth, orderedTickerGroups],
  );
  const maxTickerGroupColumn = useMemo(() => tickerGroups.reduce((acc, group) => Math.max(acc, group.column), 0), [tickerGroups]);
  const gridTemplateColumns = useMemo(() => {
    if (showTotal && maxTickerGroupColumn > 0) return `max-content repeat(${maxTickerGroupColumn}, max-content)`;
    if (showTotal) return "max-content";
    if (maxTickerGroupColumn > 0) return `repeat(${maxTickerGroupColumn}, max-content)`;
    return undefined;
  }, [maxTickerGroupColumn, showTotal]);

  const tickersRef = useRef<HTMLDivElement | null>(null);
  const dragStateRef = useRef<{
    pointerId: number;
    startX: number;
    pointerOffsetFromGroupCenter: number;
    tickerGroupId: string;
    startColumn: number;
    startTickerGroups: ResourceTickerGroupData[];
    targetColumn: number;
    active: boolean;
    captureEl: HTMLElement | null;
  } | null>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [dragTargetColumn, setDragTargetColumn] = useState<number | null>(null);
  const [dragTickerGroupId, setDragTickerGroupId] = useState<string | null>(null);

  const getTargetColumnForClientX = (clientX: number) => {
    const el = tickersRef.current;
    if (!el) return 1;

    const cells = Array.from(el.querySelectorAll<HTMLElement>("[data-ticker-group-column]"))
      .map((cell) => {
        const columnValue = Number.parseInt(cell.dataset.tickerGroupColumn ?? "", 10);
        if (!Number.isFinite(columnValue)) return null;

        const rect = cell.getBoundingClientRect();
        return {
          column: columnValue,
          centerX: rect.left + rect.width / 2,
        };
      })
      .filter((cell): cell is { column: number; centerX: number } => cell != null)
      .sort((a, b) => a.column - b.column);

    if (cells.length === 0) return 1;

    for (const cell of cells) {
      if (clientX <= cell.centerX) return cell.column;
    }

    return cells[cells.length - 1]?.column ?? 1;
  };

  const handlePointerDown = (index: number) => (e: React.PointerEvent<HTMLButtonElement>) => {
    if (!draggableTickerGroups) return;
    if (!onReorderTickerGroups) return;
    if (e.button !== 0) return;

    const tickerGroup = orderedTickerGroups[index];
    if (!tickerGroup) return;

    const captureEl = e.currentTarget.closest<HTMLElement>("[data-ticker-group-column]");
    if (!captureEl) return;

    const currentTargetRect = captureEl.getBoundingClientRect();
    const groupCenterX = currentTargetRect.left + currentTargetRect.width / 2;

    dragStateRef.current = {
      pointerId: e.pointerId,
      startX: e.clientX,
      pointerOffsetFromGroupCenter: e.clientX - groupCenterX,
      tickerGroupId: tickerGroup.id,
      startColumn: tickerGroup.column,
      startTickerGroups: orderedTickerGroups,
      targetColumn: tickerGroup.column,
      active: false,
      captureEl,
    };

    setIsDragging(false);
    setDragTargetColumn(tickerGroup.column);
    setDragTickerGroupId(tickerGroup.id);
  };

  const handlePointerMove = (e: React.PointerEvent) => {
    const state = dragStateRef.current;
    if (!state) return;
    if (e.pointerId !== state.pointerId) return;
    if (!draggableTickerGroups || !onReorderTickerGroups) return;

    const dx = e.clientX - state.startX;
    if (!state.active && Math.abs(dx) < 6) return;

    if (!state.active) {
      state.active = true;
      setIsDragging(true);

      // Capture only once a drag is confirmed, so normal clicks/double-clicks still work.
      if (state.captureEl && !state.captureEl.hasPointerCapture(e.pointerId)) {
        state.captureEl.setPointerCapture(e.pointerId);
      }
    }

    const adjustedClientX = e.clientX - state.pointerOffsetFromGroupCenter;
    const nextColumn = getTargetColumnForClientX(adjustedClientX);
    state.targetColumn = nextColumn;
    setDragTargetColumn(nextColumn);
  };

  const endDrag = (e: React.PointerEvent) => {
    const state = dragStateRef.current;
    if (!state) return;
    if (e.pointerId !== state.pointerId) return;

    if (state.active && onReorderTickerGroups) {
      const nextTickerGroups = reorderTickerGroupByColumn(state.startTickerGroups, state.tickerGroupId, state.targetColumn);
      onReorderTickerGroups(nextTickerGroups);
    }

    dragStateRef.current = null;
    setIsDragging(false);
    setDragTargetColumn(null);
    setDragTickerGroupId(null);
  };

  return (
    <div className="resource-category" data-column={column}>
      <ResourceCategoryHeader label={label} />
      <div
        ref={tickersRef}
        className={isDragging ? "resource-category__tickers resource-category__tickers--dragging" : "resource-category__tickers"}
        style={gridTemplateColumns ? { gridTemplateColumns } : undefined}
        onPointerMove={handlePointerMove}
        onPointerUp={endDrag}
        onPointerCancel={endDrag}
        onLostPointerCapture={endDrag}>
        {showTotal && (
          <div
            className={draggableTickerGroups ? "resource-category__totals resource-category__totals--with-drag-handle" : "resource-category__totals"}
            aria-label={`${label} totals`}>
            {rowTotals.map((total, rowIndex) => (
              <div key={`${label}-total-${rowIndex}`} className="resource-category__totals-row">
                <div className="resource-ticker-group__ticker-slot">
                  <ResourceTotal
                    amount={total}
                    tooltip={`${label} total ${rowIndex + 1}: ${formatNumberWithCommas(total)}`}
                    onClick={
                      onClickAmount
                        ? () =>
                            onClickAmount({
                              name: `${label} total ${rowIndex + 1}`,
                              index: -1,
                              amount: total,
                              isTotal: true,
                              rowIndex,
                            })
                        : undefined
                    }
                  />
                </div>
                {rowIndex < rowTotals.length - 1 && (
                  <div className="resource-ticker-group__arrow-slot" aria-hidden="true">
                    <span className="resource-ticker-group__arrow" />
                  </div>
                )}
              </div>
            ))}
          </div>
        )}

        {orderedTickerGroups.map((group, idx) => (
          <div
            key={group.id}
            data-ticker-group-column={group.column}
            className={[
              "resource-category__cell",
              draggableTickerGroups ? "resource-category__cell--draggable" : "",
              isDragging && dragTargetColumn === group.column ? "resource-category__cell--drop-target" : "",
              isDragging && dragTickerGroupId === group.id ? "resource-category__cell--drag-source" : "",
            ]
              .filter(Boolean)
              .join(" ")}
            style={{ gridColumnStart: (showTotal ? 1 : 0) + group.column }}>
            <ResourceTickerGroup
              label={group.label ?? label}
              tickers={group.tickers}
              rowCount={maxTickerDepth}
              tooltip={group.tickers.map((ticker) => `${ticker.name}: ${formatNumberWithCommas(ticker.amount)}`).join(" -> ")}
              draggable={draggableTickerGroups}
              onDragHandlePointerDown={handlePointerDown(idx)}
              onClickTicker={
                onClickAmount
                  ? (ticker) =>
                      onClickAmount({
                        name: ticker.name,
                        index: group.column,
                        amount: ticker.amount,
                        isTotal: false,
                      })
                  : undefined
              }
              disabled={isDragging}
            />
          </div>
        ))}
      </div>
    </div>
  );
}
