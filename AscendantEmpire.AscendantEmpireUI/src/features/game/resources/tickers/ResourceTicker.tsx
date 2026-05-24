import type { ReactNode } from "react";
import "./ResourceTicker.css";
import ResourceIndicatorPanel from "./ResourceIndicatorPanel";
import ResourceAmountLabel from "./ResourceAmountLabel";
import { getResourceTickerBackground } from "./ResourceConstants";
import { formatResourceAmount } from "@/util/number";
import { buildResourceTooltip } from "./ResourceTooltip";

export interface ResourceTickerProps {
  name: string;
  icon: ReactNode;
  amount: number;
  tooltip?: string;
  onClick?: () => void;
  disabled?: boolean;
  IsLuxury?: boolean;
  isImported?: boolean;
  isExported?: boolean;
  hasStrategicStockpile?: boolean;
  soonToBeObsolete?: boolean;
  demandNotMet?: boolean;
  amountOrientation?: "horizontal" | "vertical";
  buttonHeight?: string;
}

export default function ResourceTicker({
  name,
  icon,
  amount,
  tooltip,
  onClick,
  disabled,
  IsLuxury,
  isImported,
  isExported,
  hasStrategicStockpile,
  soonToBeObsolete,
  demandNotMet,
  amountOrientation = "horizontal",
  buttonHeight,
}: ResourceTickerProps) {
  const display = formatResourceAmount(amount);
  const tip = buildResourceTooltip({
    name,
    amount,
    tooltip,
    isImported,
    isExported,
    hasStrategicStockpile,
    soonToBeObsolete,
    demandNotMet,
  });

  const isDisabled = !!disabled;
  const isClickable = !isDisabled && !!onClick;
  const isLuxury = IsLuxury === true;
  const isVerticalAmount = amountOrientation === "vertical";
  const backgroundColor = getResourceTickerBackground({ soonToBeObsolete, demandNotMet });

  const className = [
    "resource-ticker",
    isClickable ? "resource-ticker--button" : "",
    isLuxury ? "resource-ticker--luxury" : "",
    isVerticalAmount ? "resource-ticker--vertical-amount" : "",
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <span title={tip} className={isDisabled ? "resource-ticker-wrapper resource-ticker-wrapper--disabled" : "resource-ticker-wrapper"}>
      <span className="resource-ticker-shell">
        <button
          type="button"
          className={className}
          aria-label={`${name}: ${display}`}
          onClick={isDisabled ? undefined : onClick}
          disabled={isDisabled}
          style={{ backgroundColor, height: buttonHeight }}>
          <span className="resource-ticker__icon">{icon}</span>
          <ResourceAmountLabel value={display} vertical={isVerticalAmount} />
        </button>
        <ResourceIndicatorPanel isImported={isImported} isExported={isExported} hasStrategicStockpile={hasStrategicStockpile} />
      </span>
    </span>
  );
}
