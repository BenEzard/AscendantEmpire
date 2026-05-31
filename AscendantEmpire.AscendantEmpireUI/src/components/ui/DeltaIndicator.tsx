import { formatResourceAmount } from "@/util/number";
import Tooltip from "./Tooltip";

interface DeltaIndicatorProps {
  delta: number;
  tooltip?: string;
}

export default function DeltaIndicator({ delta, tooltip }: DeltaIndicatorProps) {
  const content =
    delta === 0 ? (
      <span className="text-xs text-slate-400">—</span>
    ) : (
      <span className={`text-xs ${delta > 0 ? "text-emerald-400" : "text-rose-400"}`}>
        {delta > 0 ? "▲" : "▼"} {formatResourceAmount(Math.abs(delta))}
      </span>
    );

  return <Tooltip content={tooltip}>{content}</Tooltip>;
}
