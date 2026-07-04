import type { PlaceholderCost } from "./model/placeholderCosts";

import { getResourceIconByLabel } from "@/features/game/resources/tickers/resourceIcons";
import Tooltip from "@/components/ui/Tooltip";

function fmt(n: number) {
  if (!Number.isFinite(n)) return String(n);
  return n.toFixed(1);
}

function resourceIcon(label: string) {
  const icon = getResourceIconByLabel(label);
  const fallback = (
    <div className="flex h-full w-full items-center justify-center text-[10px] font-semibold text-white/80">{label.slice(0, 2).toUpperCase()}</div>
  );

  return (
    <Tooltip content={label}>
      <div className="mx-auto h-6 w-6 [&>img]:h-full [&>img]:w-full [&>img]:object-contain">{icon ?? fallback}</div>
    </Tooltip>
  );
}

export interface TrainingCostStatsProps {
  cost?: PlaceholderCost;
  className?: string;
}

export function TrainingCostStats({ cost, className }: TrainingCostStatsProps) {
  const entries = Object.entries(cost ?? {})
    .filter(([, v]) => Number.isFinite(v) && v > 0)
    .sort(([a], [b]) => a.localeCompare(b));

  const hasAny = entries.length > 0;

  const columns = Math.max(1, entries.length);
  const gridStyle: React.CSSProperties = {
    gridTemplateColumns: `repeat(${columns}, 56px)`,
    gridTemplateRows: "repeat(2, 24px)",
  };

  return (
    <div className={className ?? "-mx-2 rounded-md border border-white/10 bg-sky-900/30 px-3 py-2"}>
      <div className="border-b border-white/10 pb-1 text-sm font-semibold text-white">Training Cost</div>

      {!hasAny ? (
        <div className="mt-2 text-xs text-white/70">None</div>
      ) : (
        <div className="mt-2 overflow-x-auto">
          <div className="grid items-center gap-x-3 gap-y-0" style={gridStyle}>
            {entries.map(([label, amount]) => (
              <div key={label} className="row-span-2 flex h-12 w-full flex-col items-center justify-center rounded border border-white/10 box-border">
                <div className="flex h-6 items-center justify-center">{resourceIcon(label)}</div>
                <div className="flex h-6 items-center justify-center text-center text-xs tabular-nums leading-none text-white">{fmt(amount)}</div>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
