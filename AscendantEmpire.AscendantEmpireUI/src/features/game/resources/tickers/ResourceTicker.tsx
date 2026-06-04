import type { ReactNode } from "react";
import "./ResourceTicker.css";
import ResourceIndicatorPanel from "./ResourceIndicatorPanel";
import ResourceAmountLabel from "./ResourceAmountLabel";
import { getResourceTickerBackground, type ResourceGrade } from "./ResourceConstants";
import { formatResourceAmount } from "@/util/number";
import { buildResourceTooltip } from "./ResourceTooltip";
import bronzeShieldIconUrl from "@/assets/icons/bronze-shield_15px.png";
import silverShieldIconUrl from "@/assets/icons/silver-shield_15px.png";
import goldShieldIconUrl from "@/assets/icons/gold-shield_15px.png";
import Tooltip from "@/components/ui/Tooltip";

const shieldIconByGrade: Partial<Record<ResourceGrade, string>> = {
  bronze: bronzeShieldIconUrl,
  silver: silverShieldIconUrl,
  gold: goldShieldIconUrl,
};

export interface ResourceTickerProps {
  name: string;
  icon: ReactNode;
  amount: number;
  tooltip?: string;
  onClick?: () => void;
  onPointerDown?: (e: React.PointerEvent<HTMLButtonElement>) => void;
  disabled?: boolean;
  IsLuxury?: boolean;
  isImported?: boolean;
  isExported?: boolean;
  hasStrategicStockpile?: boolean;
  // Number of turns until this resource becomes obsolete; omit or 0 means not obsoleting
  obsoleteIn?: number;
  demandNotMet?: boolean;
  amountOrientation?: "horizontal" | "vertical";
  buttonHeight?: string;
  grade?: ResourceGrade;
}

export default function ResourceTicker({
  name,
  icon,
  amount,
  tooltip,
  onClick,
  onPointerDown,
  disabled,
  IsLuxury,
  isImported,
  isExported,
  hasStrategicStockpile,
  obsoleteIn,
  demandNotMet,
  amountOrientation = "horizontal",
  buttonHeight,
  grade: quality = "none",
}: ResourceTickerProps) {
  const display = formatResourceAmount(amount);
  const tip = buildResourceTooltip({
    name,
    amount,
    tooltip,
    isImported,
    isExported,
    hasStrategicStockpile,
    obsoleteIn,
    demandNotMet,
  });

  if (amount === 0) return null;

  const isDisabled = !!disabled;
  const isClickable = !isDisabled && !!onClick;
  const isLuxury = IsLuxury === true;
  const isVerticalAmount = amountOrientation === "vertical";
  const backgroundColor = getResourceTickerBackground({ obsoleteIn, demandNotMet });

  const className = [
    "resource-ticker",
    isClickable ? "resource-ticker--button" : "",
    isLuxury ? "resource-ticker--luxury" : "",
    isVerticalAmount ? "resource-ticker--vertical-amount" : "",
    quality !== "none" ? `resource-ticker--quality-${quality}` : "",
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <Tooltip content={tip}>
      <span className={isDisabled ? "resource-ticker-wrapper resource-ticker-wrapper--disabled" : "resource-ticker-wrapper"}>
        <span className="resource-ticker-shell">
          <ResourceIndicatorPanel isImported={isImported} isExported={isExported} hasStrategicStockpile={hasStrategicStockpile} />
          <button
            type="button"
            className={className}
            aria-label={`${name}: ${display}`}
            onClick={isDisabled ? undefined : onClick}
            onPointerDown={isDisabled ? undefined : onPointerDown}
            onDragStart={(e) => e.preventDefault()}
            disabled={isDisabled}
            style={{ backgroundColor, height: buttonHeight }}>
            <span className="resource-ticker__icon">{icon}</span>
            {shieldIconByGrade[quality] && (
              <img src={shieldIconByGrade[quality]} alt={quality} className="resource-ticker__quality-shield" aria-hidden="true" draggable={false} />
            )}
            <ResourceAmountLabel value={display} vertical={isVerticalAmount} />
          </button>
        </span>
      </span>
    </Tooltip>
  );
}
