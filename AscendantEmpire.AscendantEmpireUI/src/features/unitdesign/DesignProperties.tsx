import type { DesignAggregate } from "./model/unitDesign";
import { BattleStats } from "./BattleStats";
import { UpkeepStats } from "./UpkeepStats";
import { TrainingCostStats } from "./TrainingCostStats";
import { rawToScaled } from "./types/UnitComponent";

export interface DesignPropertiesProps {
  aggregate: DesignAggregate;
}

/** A labelled Scale-1,000 summary row (values are raw and shown as decimals). */
function SummaryRow({ label, raw }: { label: string; raw: number }) {
  return (
    <div className="flex items-center justify-between gap-3">
      <div className="text-white/80">{label}</div>
      <div className="font-mono tabular-nums text-white">{rawToScaled(raw).toFixed(2)}</div>
    </div>
  );
}

export default function DesignProperties({ aggregate }: DesignPropertiesProps) {
  const { summary, battleStats, cost, upkeep } = aggregate;

  const showBattleStats =
    Object.values(battleStats.attack ?? {}).some((n) => Number.isFinite(n) && n > 0) ||
    Object.values(battleStats.resistance ?? {}).some((n) => Number.isFinite(n) && n > 0);

  return (
    <div className="flex h-full w-full flex-col border-l border-white/10 bg-sky-900/20" style={{ minHeight: 0 }}>
      <div className="border-b border-white/10 px-4 py-3 text-sm font-semibold text-white">Design Properties</div>

      <div className="flex-1 overflow-auto px-4 py-3 text-sm text-white" style={{ minHeight: 0 }}>
        <div className="space-y-3">
          {/* Real, canonical-backed summary stats. */}
          <div className="-mx-2 space-y-1 rounded-md border border-white/10 bg-sky-900/30 px-3 py-2">
            <div className="border-b border-white/10 pb-1 text-sm font-semibold text-white">Summary</div>
            <div className="mt-1 space-y-1">
              <SummaryRow label="Weight" raw={summary.weight} />
              <SummaryRow label="Movement" raw={summary.movementPoints} />
              <SummaryRow label="Hitpoints" raw={summary.hitpoints} />
              <SummaryRow label="Detection" raw={summary.detection} />
              <SummaryRow label="Visibility" raw={summary.visibility} />
            </div>
          </div>

          {/* PLACEHOLDER_COST_UPKEEP: cost/upkeep aren't modelled server-side yet — shown from stand-ins. */}
          <TrainingCostStats cost={cost} />

          <UpkeepStats upkeep={upkeep} />

          {showBattleStats && <BattleStats stats={battleStats} className="-mx-2 rounded-md border border-white/10 bg-sky-900/30 px-3 py-2" />}
        </div>
      </div>
    </div>
  );
}
