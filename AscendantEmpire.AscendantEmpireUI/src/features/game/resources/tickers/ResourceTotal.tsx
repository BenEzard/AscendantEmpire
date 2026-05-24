import "./ResourceTicker.css";
import { formatNumberWithCommas } from "@/util/number";
import ResourceAmountLabel from "./ResourceAmountLabel";
import { formatResourceAmount } from "@/util/number";

export interface ResourceTotalProps {
  amount: number;
  tooltip?: string;
  onClick?: () => void;
  disabled?: boolean;
  variant?: "square" | "compact";
}

export default function ResourceTotal({ amount, tooltip, onClick, disabled, variant = "square" }: ResourceTotalProps) {
  const display = formatResourceAmount(amount);
  const tip = tooltip ?? formatNumberWithCommas(amount);

  const isDisabled = !!disabled;
  const isClickable = !isDisabled && !!onClick;
  const isCompact = variant === "compact";

  const className = [
    "resource-ticker",
    "resource-ticker--total",
    isCompact ? "resource-ticker--compact-total" : "",
    isClickable ? "resource-ticker--button" : "",
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <span
      title={tip}
      className={[
        isDisabled ? "resource-ticker-wrapper resource-ticker-wrapper--disabled" : "resource-ticker-wrapper",
        isCompact ? "resource-ticker-wrapper--compact" : "",
      ]
        .filter(Boolean)
        .join(" ")}>
      <button type="button" className={className} aria-label={`${display}`} onClick={isDisabled ? undefined : onClick} disabled={isDisabled}>
        {isCompact ? (
          <ResourceAmountLabel value={display} className="resource-amount-label--static" />
        ) : (
          <span className="resource-total__amount">{display}</span>
        )}
      </button>
    </span>
  );
}
