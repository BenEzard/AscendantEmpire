import { Fragment } from "react";
import { formatNumberWithCommas } from "@/util/number";
import ResourceTicker from "./ResourceTicker";
import "./ResourceTickerCollection.css";
import type { ResourceTickerData } from "./ResourceTickerCollection";

export interface ProcessedResourceTickerGroupProps {
  label: string;
  tickers: ResourceTickerData[];
  rowCount: number;
  tooltip?: string;
  draggable?: boolean;
  disabled?: boolean;
  onDragHandlePointerDown?: (e: React.PointerEvent<HTMLButtonElement>) => void;
  onClickTicker?: (ticker: ResourceTickerData, tickerIndex: number) => void;
}

export default function ProcessedResourceTickerGroup({
  label,
  tickers,
  rowCount,
  tooltip,
  draggable = false,
  disabled = false,
  onDragHandlePointerDown,
  onClickTicker,
}: ProcessedResourceTickerGroupProps) {
  const rows = Array.from({ length: rowCount }, (_, rowIndex) => tickers[rowIndex] ?? null);
  const groupTooltip = tooltip ?? tickers.map((ticker) => `${ticker.name}: ${formatNumberWithCommas(ticker.amount)}`).join(" -> ");
  const shouldStretchSingleTicker = tickers.length === 1 && rowCount > 1 && tickers[0] != null;

  return (
    <div className="resource-ticker-group" role="group" aria-label={label} title={groupTooltip}>
      {draggable && (
        <button
          type="button"
          className="resource-ticker-group__drag-handle"
          aria-label={`Drag ${label}`}
          onPointerDown={onDragHandlePointerDown}
          disabled={disabled}>
          <span className="resource-ticker-group__drag-line" aria-hidden="true" />
        </button>
      )}

      {rows.map((ticker, rowIndex) => {
        const isHiddenPlaceholderRow = shouldStretchSingleTicker && rowIndex > 0;
        const rowClassName = [
          "resource-ticker-group__ticker-slot",
          shouldStretchSingleTicker && rowIndex === 0 ? "resource-ticker-group__ticker-slot--single-top-aligned" : "",
          isHiddenPlaceholderRow ? "resource-ticker-group__ticker-slot--hidden" : "",
        ]
          .filter(Boolean)
          .join(" ");

        const arrowClassName = ["resource-ticker-group__arrow-slot", shouldStretchSingleTicker ? "resource-ticker-group__arrow-slot--hidden" : ""]
          .filter(Boolean)
          .join(" ");

        const effectiveTicker = shouldStretchSingleTicker && rowIndex === 0 ? tickers[0] : ticker;

        return (
          <Fragment key={effectiveTicker?.id ?? `placeholder-${rowIndex}`}>
            <div className={rowClassName} aria-hidden={isHiddenPlaceholderRow ? "true" : undefined}>
              {effectiveTicker ? (
                <ResourceTicker
                  name={effectiveTicker.name}
                  icon={effectiveTicker.icon}
                  amount={effectiveTicker.amount}
                  tooltip={effectiveTicker.tooltip ?? `${effectiveTicker.name}: ${formatNumberWithCommas(effectiveTicker.amount)}`}
                  IsLuxury={effectiveTicker.isLuxury === true}
                  isImported={effectiveTicker.isImported}
                  isExported={effectiveTicker.isExported}
                  hasStrategicStockpile={effectiveTicker.hasStrategicStockpile}
                  soonToBeObsolete={effectiveTicker.soonToBeObsolete}
                  demandNotMet={effectiveTicker.demandNotMet}
                  onClick={onClickTicker ? () => onClickTicker(effectiveTicker, rowIndex) : undefined}
                  disabled={disabled}
                />
              ) : (
                <span className="resource-ticker-group__placeholder" aria-hidden="true" />
              )}
            </div>
            {rowIndex < rowCount - 1 && (
              <div className={arrowClassName} aria-hidden="true">
                {ticker != null && tickers[rowIndex + 1] != null && !shouldStretchSingleTicker ? (
                  <span className="resource-ticker-group__arrow" />
                ) : (
                  <span className="resource-ticker-group__arrow-spacer" />
                )}
              </div>
            )}
          </Fragment>
        );
      })}
    </div>
  );
}
