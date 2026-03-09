import { Fragment } from "react";
import { formatNumberWithCommas } from "@/util/number";
import ResourceTicker from "./ResourceTicker";
import "./ResourceTickerCollection.css";
import type { ResourceTickerData } from "./ResourceTickerCollection";

export interface ResourceTickerGroupProps {
  label: string;
  tickers: ResourceTickerData[];
  rowCount: number;
  tooltip?: string;
  draggable?: boolean;
  disabled?: boolean;
  onDragHandlePointerDown?: (e: React.PointerEvent<HTMLButtonElement>) => void;
  onClickTicker?: (ticker: ResourceTickerData, tickerIndex: number) => void;
}

export default function ResourceTickerGroup({
  label,
  tickers,
  rowCount,
  tooltip,
  draggable = false,
  disabled = false,
  onDragHandlePointerDown,
  onClickTicker,
}: ResourceTickerGroupProps) {
  const rows = Array.from({ length: rowCount }, (_, rowIndex) => tickers[rowIndex] ?? null);
  const groupTooltip = tooltip ?? tickers.map((ticker) => `${ticker.name}: ${formatNumberWithCommas(ticker.amount)}`).join(" -> ");

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
      {rows.map((ticker, rowIndex) => (
        <Fragment key={ticker?.id ?? `placeholder-${rowIndex}`}>
          <div className="resource-ticker-group__ticker-slot">
            {ticker ? (
              <ResourceTicker
                name={ticker.name}
                icon={ticker.icon}
                amount={ticker.amount}
                tooltip={ticker.tooltip ?? `${ticker.name}: ${formatNumberWithCommas(ticker.amount)}`}
                IsLuxury={ticker.isLuxury === true}
                onClick={onClickTicker ? () => onClickTicker(ticker, rowIndex) : undefined}
                disabled={disabled}
              />
            ) : (
              <span className="resource-ticker-group__placeholder" aria-hidden="true" />
            )}
          </div>
          {rowIndex < rowCount - 1 && (
            <div className="resource-ticker-group__arrow-slot" aria-hidden="true">
              {ticker != null && tickers[rowIndex + 1] != null ? (
                <span className="resource-ticker-group__arrow" />
              ) : (
                <span className="resource-ticker-group__arrow-spacer" />
              )}
            </div>
          )}
        </Fragment>
      ))}
    </div>
  );
}
