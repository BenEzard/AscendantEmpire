import type { ReactNode } from "react";
import "./ResourceTicker.css";
import { formatNumberWithCommas } from "@/util/number";

export interface ResourceTickerProps {
  name: string;
  icon: ReactNode;
  amount: number;
  tooltip?: string;
  onClick?: () => void;
  disabled?: boolean;
  IsLuxury?: boolean;
}

function formatAmount(amount: number): string {
  if (!Number.isFinite(amount)) return "0";

  const whole = Math.floor(amount);
  if (whole < 1000) return `${whole}`;

  if (whole < 100001) {
    const k = Math.floor(whole / 1000);
    return `${k}K`;
  }

  // Fallback for larger values; keep it compact.
  const m = Math.floor(whole / 1_000_000);
  if (m >= 1) return `${m}M`;

  const k = Math.floor(whole / 1000);
  return `${k}K`;
}

export default function ResourceTicker({ name, icon, amount, tooltip, onClick, disabled, IsLuxury }: ResourceTickerProps) {
  const display = formatAmount(amount);
  const tip = tooltip ?? formatNumberWithCommas(amount);

  const isDisabled = !!disabled;
  const isClickable = !isDisabled && !!onClick;
  const isLuxury = IsLuxury === true;

  const className = ["resource-ticker", isClickable ? "resource-ticker--button" : "", isLuxury ? "resource-ticker--luxury" : ""]
    .filter(Boolean)
    .join(" ");

  return (
    <span title={tip} className={isDisabled ? "resource-ticker-wrapper resource-ticker-wrapper--disabled" : "resource-ticker-wrapper"}>
      <button type="button" className={className} aria-label={`${name}: ${display}`} onClick={isDisabled ? undefined : onClick} disabled={isDisabled}>
        <span className="resource-ticker__icon">{icon}</span>
        <span className="resource-ticker__amount">{display}</span>
      </button>
    </span>
  );
}
