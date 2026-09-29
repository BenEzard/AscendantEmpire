import "./ResourceTicker.css";
import ResourceIconImage from "../ResourceIconImage";
import ResourceIndicatorPanel from "./ResourceIndicatorPanel";
import ResourceAmountLabel from "./ResourceAmountLabel";
import { getResourceTickerBackground, type ResourceGrade } from "./ResourceConstants";
import { formatResourceAmount } from "@/util/number";
import { buildResourceTooltip } from "./ResourceTooltip";
import bronzeShieldIconUrl from "@/assets/icons/bronze-shield_15px.png";
import silverShieldIconUrl from "@/assets/icons/silver-shield_15px.png";
import goldShieldIconUrl from "@/assets/icons/gold-shield_15px.png";
import Tooltip from "@/components/ui/Tooltip";

// The medals are presentation and the grades are mechanics, so the keys are the API's ResourceGrade names while the images keep their metal
// filenames. "None" is deliberately absent — an ungraded resource wears no shield.
const shieldIconByGrade: Partial<Record<ResourceGrade, string>> = {
  Low: bronzeShieldIconUrl,
  Medium: silverShieldIconUrl,
  High: goldShieldIconUrl,
};

export interface ResourceTickerProps {
  name: string;
  /** Root-relative URL of the resource's catalogue icon, or "" to fall back to the generic glyph. */
  iconPath: string;
  amount: number;
  tooltip?: string;
  onClick?: () => void;
  onPointerDown?: (e: React.PointerEvent<HTMLButtonElement>) => void;
  disabled?: boolean;
  IsLuxury?: boolean;
  isImported?: boolean;
  isExported?: boolean;
  hasStrategicStockpile?: boolean;
  // Number of rounds until this resource becomes obsolete; omit or 0 means not obsoleting
  obsoleteIn?: number;
  demandNotMet?: boolean;
  amountOrientation?: "horizontal" | "vertical";
  buttonHeight?: string;
  grade?: ResourceGrade;
}

export default function ResourceTicker({
  name,
  iconPath,
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
  grade = "None",
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
    // No stylesheet defines these: the grade is shown by its shield, and the class is a hook for tests and for styling a grade later.
    grade !== "None" ? `resource-ticker--grade-${grade}` : "",
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
            <span className="resource-ticker__icon">
              <ResourceIconImage iconPath={iconPath} />
            </span>
            {shieldIconByGrade[grade] && (
              <img src={shieldIconByGrade[grade]} alt={grade} className="resource-ticker__quality-shield" aria-hidden="true" draggable={false} />
            )}
            <ResourceAmountLabel value={display} vertical={isVerticalAmount} />
          </button>
        </span>
      </span>
    </Tooltip>
  );
}
