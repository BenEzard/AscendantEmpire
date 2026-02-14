import { useMemo, useRef, useState, type ReactNode } from "react";
import ResourceTicker from "./ResourceTicker";
import ResourceTotal from "./ResourceTotal";
import ResourceCategoryHeader from "./ResourceCategoryHeader";
import "./ResourceTickerCollection.css";
import type { ResourceTickerData } from "./ResourceTickerCollection";
import { formatNumberWithCommas } from "@/util/number";

export interface ResourceCategoryProps {
  label: string;
  column: number;
  icon: ReactNode;
  tickers: ResourceTickerData[];
  showTotal?: boolean;
  onClickAmount?: (args: { label: string; index: number; amount: number; isTotal: boolean }) => void;
  draggableTickers?: boolean;
  onReorderTickers?: (tickers: ResourceTickerData[]) => void;
}

function sortTickersByColumn(tickers: ResourceTickerData[]): ResourceTickerData[] {
  return tickers.slice().sort((a, b) => {
    const ac = Number.isFinite(a.column) ? a.column : Number.POSITIVE_INFINITY;
    const bc = Number.isFinite(b.column) ? b.column : Number.POSITIVE_INFINITY;
    return ac - bc;
  });
}

function reorderTickerByColumn(tickers: ResourceTickerData[], draggedId: string, targetColumn: number): ResourceTickerData[] {
  const ordered = sortTickersByColumn(tickers);
  const fromIndex = ordered.findIndex((t) => t.id === draggedId);
  if (fromIndex < 0) return ordered;

  const toIndex = Math.max(0, Math.min(ordered.length - 1, targetColumn - 1));
  if (fromIndex === toIndex) return ordered;

  const next = ordered.slice();
  const [moved] = next.splice(fromIndex, 1);
  next.splice(toIndex, 0, moved);

  return next.map((t, idx) => ({ ...t, column: idx + 1 }));
}

export default function ResourceCategory({
  label,
  column,
  icon,
  tickers,
  showTotal = true,
  onClickAmount,
  draggableTickers = false,
  onReorderTickers,
}: ResourceCategoryProps) {
  const orderedTickers = useMemo(() => sortTickersByColumn(tickers), [tickers]);
  const total = useMemo(() => orderedTickers.reduce((acc, t) => acc + t.amount, 0), [orderedTickers]);
  const maxTickerColumn = useMemo(() => tickers.reduce((acc, t) => Math.max(acc, t.column), 0), [tickers]);
  const cols = (showTotal ? 1 : 0) + maxTickerColumn;

  const tickersRef = useRef<HTMLDivElement | null>(null);
  const dragStateRef = useRef<{
    pointerId: number;
    startX: number;
    tickerId: string;
    startColumn: number;
    startTickers: ResourceTickerData[];
    targetColumn: number;
    active: boolean;
    captureEl: HTMLElement | null;
  } | null>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [dragTargetColumn, setDragTargetColumn] = useState<number | null>(null);
  const [dragTickerId, setDragTickerId] = useState<string | null>(null);

  const getStepSizePx = () => {
    const el = tickersRef.current;
    if (!el) return 40;
    const firstCell = el.querySelector<HTMLElement>("[data-amount-cell='1']");
    const cellWidth = firstCell?.getBoundingClientRect().width ?? 40;
    const styles = window.getComputedStyle(el);
    const gapRaw = styles.columnGap || styles.gap || "0";
    const gapPx = Number.parseFloat(gapRaw) || 0;
    return cellWidth + gapPx;
  };

  const handlePointerDown = (index: number) => (e: React.PointerEvent) => {
    if (!draggableTickers) return;
    if (!onReorderTickers) return;
    if (e.button !== 0) return;

    const ticker = orderedTickers[index];
    if (!ticker) return;

    dragStateRef.current = {
      pointerId: e.pointerId,
      startX: e.clientX,
      tickerId: ticker.id,
      startColumn: ticker.column,
      startTickers: orderedTickers,
      targetColumn: ticker.column,
      active: false,
      captureEl: e.currentTarget instanceof HTMLElement ? e.currentTarget : null,
    };

    setIsDragging(false);
    setDragTargetColumn(ticker.column);
    setDragTickerId(ticker.id);
  };

  const handlePointerMove = (e: React.PointerEvent) => {
    const state = dragStateRef.current;
    if (!state) return;
    if (e.pointerId !== state.pointerId) return;
    if (!draggableTickers || !onReorderTickers) return;

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

    const step = getStepSizePx();
    const deltaCols = Math.round(dx / step);
    const nextColumn = Math.max(1, Math.min(state.startTickers.length, state.startColumn + deltaCols));
    state.targetColumn = nextColumn;
    setDragTargetColumn(nextColumn);
  };

  const endDrag = (e: React.PointerEvent) => {
    const state = dragStateRef.current;
    if (!state) return;
    if (e.pointerId !== state.pointerId) return;

    if (state.active && onReorderTickers) {
      const nextTickers = reorderTickerByColumn(state.startTickers, state.tickerId, state.targetColumn);
      onReorderTickers(nextTickers);
    }

    dragStateRef.current = null;
    setIsDragging(false);
    setDragTargetColumn(null);
    setDragTickerId(null);
  };

  return (
    <div className="resource-category" data-column={column}>
      <ResourceCategoryHeader label={label} />
      <div
        ref={tickersRef}
        className={isDragging ? "resource-category__tickers resource-category__tickers--dragging" : "resource-category__tickers"}
        style={{ gridTemplateColumns: `repeat(${cols}, var(--rt-size))` }}
        onPointerMove={handlePointerMove}
        onPointerUp={endDrag}
        onPointerCancel={endDrag}
        onLostPointerCapture={endDrag}>
        {showTotal && (
          <ResourceTotal
            amount={total}
            tooltip={`${label} total: ${formatNumberWithCommas(total)}`}
            onClick={
              onClickAmount
                ? () =>
                    onClickAmount({
                      label,
                      index: -1,
                      amount: total,
                      isTotal: true,
                    })
                : undefined
            }
          />
        )}

        {orderedTickers.map((t, idx) => (
          <div
            key={t.id}
            data-amount-cell="1"
            className={[
              "resource-category__cell",
              draggableTickers ? "resource-category__cell--draggable" : "",
              isDragging && dragTargetColumn === t.column ? "resource-category__cell--drop-target" : "",
              isDragging && dragTickerId === t.id ? "resource-category__cell--drag-source" : "",
            ]
              .filter(Boolean)
              .join(" ")}
            style={{ gridColumnStart: (showTotal ? 1 : 0) + t.column }}
            onPointerDown={handlePointerDown(idx)}>
            <ResourceTicker
              icon={t.icon ?? icon}
              iconColor={t.iconColor}
              amount={t.amount}
              tooltip={`${t.label ?? label}: ${formatNumberWithCommas(t.amount)}`}
              IsLuxury={t.isLuxury === true}
              onClick={
                onClickAmount
                  ? () =>
                      onClickAmount({
                        label,
                        index: t.column,
                        amount: t.amount,
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
