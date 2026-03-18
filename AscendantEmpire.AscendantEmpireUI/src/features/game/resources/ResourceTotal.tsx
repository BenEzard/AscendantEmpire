import "./ResourceTicker.css";
import { formatNumberWithCommas } from "@/util/number";

export interface ResourceTotalProps {
  amount: number;
  tooltip?: string;
  onClick?: () => void;
  disabled?: boolean;
}

function formatAmount(amount: number): string {
  if (!Number.isFinite(amount)) return "0";

  const whole = Math.floor(amount);
  if (whole < 1000) return `${whole}`;

  if (whole < 100001) {
    const k = Math.floor(whole / 1000);
    return `${k}K`;
  }

  const m = Math.floor(whole / 1_000_000);
  if (m >= 1) return `${m}M`;

  const k = Math.floor(whole / 1000);
  return `${k}K`;
}

export default function ResourceTotal({ amount, tooltip, onClick, disabled }: ResourceTotalProps) {
  const display = formatAmount(amount);
  const tip = tooltip ?? formatNumberWithCommas(amount);

  const isDisabled = !!disabled;
  const isClickable = !isDisabled && !!onClick;

  const className = ["resource-ticker", "resource-ticker--total", isClickable ? "resource-ticker--button" : ""].filter(Boolean).join(" ");

  return (
    <span title={tip} className={isDisabled ? "resource-ticker-wrapper resource-ticker-wrapper--disabled" : "resource-ticker-wrapper"}>
      <button type="button" className={className} aria-label={`${display}`} onClick={isDisabled ? undefined : onClick} disabled={isDisabled}>
        <span className="resource-total__amount">{display}</span>
      </button>
    </span>
  );
}
