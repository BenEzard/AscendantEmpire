import "./AssetTicker.css";
import AssetIconImage from "../AssetIconImage";
import AssetIndicatorPanel from "./AssetIndicatorPanel";
import AssetAmountLabel from "./AssetAmountLabel";
import { getAssetTickerBackground, type AssetGrade } from "./AssetConstants";
import { formatResourceAmount } from "@/util/number";
import { buildAssetTooltip } from "./AssetTooltip";
import bronzeShieldIconUrl from "@/assets/icons/bronze-shield_15px.png";
import silverShieldIconUrl from "@/assets/icons/silver-shield_15px.png";
import goldShieldIconUrl from "@/assets/icons/gold-shield_15px.png";
import Tooltip from "@/components/ui/Tooltip";

// The medals are presentation and the grades are mechanics, so the keys are the API's AssetGrade names while the images keep their metal
// filenames. "None" is deliberately absent — an ungraded asset wears no shield.
const shieldIconByGrade: Partial<Record<AssetGrade, string>> = {
  Low: bronzeShieldIconUrl,
  Medium: silverShieldIconUrl,
  High: goldShieldIconUrl,
};

export interface AssetTickerProps {
  name: string;
  /** Root-relative URL of the asset's catalogue icon, or "" to fall back to the generic glyph. */
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
  // Number of rounds until this asset becomes obsolete; omit or 0 means not obsoleting
  obsoleteIn?: number;
  demandNotMet?: boolean;
  amountOrientation?: "horizontal" | "vertical";
  buttonHeight?: string;
  grade?: AssetGrade;
}

export default function AssetTicker({
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
}: AssetTickerProps) {
  const display = formatResourceAmount(amount);
  const tip = buildAssetTooltip({
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
  const backgroundColor = getAssetTickerBackground({ obsoleteIn, demandNotMet });

  const className = [
    "asset-ticker",
    isClickable ? "asset-ticker--button" : "",
    isLuxury ? "asset-ticker--luxury" : "",
    isVerticalAmount ? "asset-ticker--vertical-amount" : "",
    // No stylesheet defines these: the grade is shown by its shield, and the class is a hook for tests and for styling a grade later.
    grade !== "None" ? `asset-ticker--grade-${grade}` : "",
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <Tooltip content={tip}>
      <span className={isDisabled ? "asset-ticker-wrapper asset-ticker-wrapper--disabled" : "asset-ticker-wrapper"}>
        <span className="asset-ticker-shell">
          <AssetIndicatorPanel isImported={isImported} isExported={isExported} hasStrategicStockpile={hasStrategicStockpile} />
          <button
            type="button"
            className={className}
            aria-label={`${name}: ${display}`}
            onClick={isDisabled ? undefined : onClick}
            onPointerDown={isDisabled ? undefined : onPointerDown}
            onDragStart={(e) => e.preventDefault()}
            disabled={isDisabled}
            style={{ backgroundColor, height: buttonHeight }}>
            <span className="asset-ticker__icon">
              <AssetIconImage iconPath={iconPath} />
            </span>
            {shieldIconByGrade[grade] && (
              <img src={shieldIconByGrade[grade]} alt={grade} className="asset-ticker__quality-shield" aria-hidden="true" draggable={false} />
            )}
            <AssetAmountLabel value={display} vertical={isVerticalAmount} />
          </button>
        </span>
      </span>
    </Tooltip>
  );
}
