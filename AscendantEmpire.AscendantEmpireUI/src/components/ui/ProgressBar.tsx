import Tooltip from "./Tooltip";

interface ProgressBarProps {
  totalDisplayWidth: number;
  completedAmount: number;
  totalAmount: number;
  tooltip?: string;
  showPercentage?: boolean;
  percentageColor?: string;
}

export default function ProgressBar({
  totalDisplayWidth,
  completedAmount,
  totalAmount,
  tooltip,
  showPercentage = true,
  percentageColor = "white",
}: ProgressBarProps) {
  const pct = totalAmount > 0 ? Math.min(completedAmount / totalAmount, 1) : 0;
  const filledWidth = Math.round(totalDisplayWidth * pct);
  const remainderWidth = totalDisplayWidth - filledWidth;

  const bar = (
    <div className="relative flex h-3 overflow-hidden rounded" style={{ width: totalDisplayWidth }}>
      {filledWidth > 0 && <div className="bg-emerald-800" style={{ width: filledWidth }} />}
      {remainderWidth > 0 && <div className="bg-[#b8c3ce]" style={{ width: remainderWidth }} />}
      {showPercentage && (
        <span
          className="absolute inset-0 flex items-center justify-center text-[9px] font-semibold leading-none"
          style={{ color: percentageColor, textShadow: "0 0 2px rgba(0,0,0,0.6)" }}>
          {Math.round(pct * 100)}%
        </span>
      )}
    </div>
  );

  return <Tooltip content={tooltip}>{bar}</Tooltip>;
}
