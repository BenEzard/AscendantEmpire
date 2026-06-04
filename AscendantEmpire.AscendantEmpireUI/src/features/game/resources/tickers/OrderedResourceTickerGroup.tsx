import { useRef, useState } from "react";
import { formatNumberWithCommas } from "@/util/number";
import OrderedResourceTicker from "./OrderedResourceTicker";
import "./ResourceTickerCollection.css";
import type { ResourceTickerData } from "./ResourceTickerCollection";

export interface OrderedResourceTickerGroupProps {
  label: string;
  tickers: ResourceTickerData[];
  draggableTickers?: boolean;
  disabled?: boolean;
  onReorderTickers?: (tickers: ResourceTickerData[]) => void;
  onClickTicker?: (ticker: ResourceTickerData, tickerIndex: number) => void;
}

function reorderTickers(tickers: ResourceTickerData[], fromIndex: number, toIndex: number): ResourceTickerData[] {
  if (fromIndex === toIndex) return tickers;
  const next = tickers.slice();
  const [moved] = next.splice(fromIndex, 1);
  next.splice(toIndex, 0, moved);
  return next;
}

export default function OrderedResourceTickerGroup({
  label,
  tickers,
  draggableTickers = false,
  disabled = false,
  onReorderTickers,
  onClickTicker,
}: OrderedResourceTickerGroupProps) {
  const cols = tickers;

  const containerRef = useRef<HTMLDivElement | null>(null);
  const dragStateRef = useRef<{
    pointerId: number;
    startX: number;
    fromIndex: number;
    targetIndex: number;
    startTickers: ResourceTickerData[];
    active: boolean;
    captureEl: HTMLElement | null;
  } | null>(null);
  const suppressClickRef = useRef(false);
  const [isDragging, setIsDragging] = useState(false);
  const [dragSourceIndex, setDragSourceIndex] = useState<number | null>(null);
  const [dragTargetIndex, setDragTargetIndex] = useState<number | null>(null);

  const getTargetIndexForClientX = (clientX: number): number => {
    const el = containerRef.current;
    if (!el) return 0;
    const slots = Array.from(el.querySelectorAll<HTMLElement>("[data-ticker-index]"))
      .map((slot) => {
        const idx = Number.parseInt(slot.dataset.tickerIndex ?? "", 10);
        if (!Number.isFinite(idx)) return null;
        const rect = slot.getBoundingClientRect();
        return { index: idx, centerX: rect.left + rect.width / 2 };
      })
      .filter((s): s is { index: number; centerX: number } => s != null)
      .sort((a, b) => a.index - b.index);

    if (slots.length === 0) return 0;
    for (const slot of slots) {
      if (clientX <= slot.centerX) return slot.index;
    }
    return slots[slots.length - 1]?.index ?? 0;
  };

  const handlePointerDown = (index: number) => (e: React.PointerEvent<HTMLButtonElement>) => {
    if (!draggableTickers || !onReorderTickers) return;
    if (e.button !== 0) return;

    const captureEl = e.currentTarget.closest<HTMLElement>("[data-ticker-index]");
    if (!captureEl) return;

    dragStateRef.current = {
      pointerId: e.pointerId,
      startX: e.clientX,
      fromIndex: index,
      targetIndex: index,
      startTickers: tickers,
      active: false,
      captureEl,
    };

    setDragSourceIndex(index);
    setDragTargetIndex(index);
    setIsDragging(false);
  };

  const handlePointerMove = (e: React.PointerEvent) => {
    const state = dragStateRef.current;
    if (!state || e.pointerId !== state.pointerId) return;

    const dx = e.clientX - state.startX;
    if (!state.active && Math.abs(dx) < 6) return;

    if (!state.active) {
      state.active = true;
      setIsDragging(true);
      if (state.captureEl && !state.captureEl.hasPointerCapture(e.pointerId)) {
        state.captureEl.setPointerCapture(e.pointerId);
      }
    }

    const nextIndex = getTargetIndexForClientX(e.clientX);
    state.targetIndex = nextIndex;
    setDragTargetIndex(nextIndex);
  };

  const endDrag = (e: React.PointerEvent) => {
    const state = dragStateRef.current;
    if (!state || e.pointerId !== state.pointerId) return;

    if (state.active && onReorderTickers) {
      suppressClickRef.current = true;
      onReorderTickers(reorderTickers(state.startTickers, state.fromIndex, state.targetIndex));
    } else if (state.active) {
      suppressClickRef.current = true;
    }

    dragStateRef.current = null;
    setIsDragging(false);
    setDragSourceIndex(null);
    setDragTargetIndex(null);
  };

  if (tickers.every((t) => t.amount === 0)) return null;

  return (
    <div
      ref={containerRef}
      className={["resource-ticker-group", isDragging ? "resource-ticker-group--dragging" : ""].filter(Boolean).join(" ")}
      role="group"
      aria-label={label}
      onPointerMove={draggableTickers ? handlePointerMove : undefined}
      onPointerUp={draggableTickers ? endDrag : undefined}
      onPointerCancel={draggableTickers ? endDrag : undefined}
      onLostPointerCapture={draggableTickers ? endDrag : undefined}>
      <div className="resource-ticker-group__tickers">
        {cols.map((ticker, colIndex) => {
          const isPlaceholder = ticker == null;
          const slotClassName = [
            "resource-ticker-group__ticker-slot",
            isPlaceholder ? "resource-ticker-group__ticker-slot--hidden" : "",
            !isPlaceholder && ticker.amount === 0 ? "resource-ticker-group__ticker-slot--zero" : "",
          ]
            .filter(Boolean)
            .join(" ");

          return (
            <div
              key={ticker?.code ?? `placeholder-${colIndex}`}
              className={slotClassName}
              data-ticker-index={colIndex}
              aria-hidden={isPlaceholder ? "true" : undefined}>
              {ticker ? (
                <OrderedResourceTicker
                  name={ticker.name}
                  icon={ticker.icon}
                  amount={ticker.amount}
                  tooltip={ticker.tooltip ?? `${ticker.name}: ${formatNumberWithCommas(ticker.amount)}`}
                  IsLuxury={ticker.isLuxury === true}
                  isImported={ticker.isImported}
                  isExported={ticker.isExported}
                  hasStrategicStockpile={ticker.hasStrategicStockpile}
                  obsoleteIn={ticker.obsoleteIn}
                  demandNotMet={ticker.demandNotMet}
                  grade={ticker.quality}
                  draggable={draggableTickers}
                  onDragHandlePointerDown={draggableTickers ? handlePointerDown(colIndex) : undefined}
                  isDragSource={isDragging && dragSourceIndex === colIndex}
                  isDropTarget={isDragging && dragTargetIndex === colIndex && dragSourceIndex !== colIndex}
                  onClick={
                    onClickTicker
                      ? () => {
                          if (suppressClickRef.current) {
                            suppressClickRef.current = false;
                            return;
                          }
                          onClickTicker(ticker, colIndex);
                        }
                      : undefined
                  }
                  disabled={disabled}
                />
              ) : (
                <span className="resource-ticker-group__placeholder" aria-hidden="true" />
              )}
            </div>
          );
        })}
      </div>
      <span className="resource-ticker-group__label">{label}</span>
    </div>
  );
}
