import type { ComponentCost, ComponentUpkeep, WeaponStats } from "./types/UnitComponent";
import { BattleStats } from "./BattleStats";
import { UpkeepStats } from "./UpkeepStats";
import { TrainingCostStats } from "./TrainingCostStats";

export interface DesignPropertiesProps {
  totalCost: ComponentCost;
  totalUpkeep: ComponentUpkeep;
  totalBattleStats: WeaponStats;
}

export default function DesignProperties({ totalCost, totalUpkeep, totalBattleStats }: DesignPropertiesProps) {
  const showBattleStats =
    Object.values(totalBattleStats.attack).some((n) => Number.isFinite(n) && n > 0) ||
    Object.values(totalBattleStats.defense).some((n) => Number.isFinite(n) && n > 0);

  return (
    <div className="flex h-full w-full flex-col border-l border-white/10 bg-sky-900/20" style={{ minHeight: 0 }}>
      <div className="border-b border-white/10 px-4 py-3 text-sm font-semibold text-white">Design Properties</div>

      <div className="flex-1 overflow-auto px-4 py-3 text-sm text-white" style={{ minHeight: 0 }}>
        <div className="space-y-3">
          <TrainingCostStats cost={totalCost} />

          <UpkeepStats upkeep={totalUpkeep} />

          {showBattleStats && <BattleStats stats={totalBattleStats} className="-mx-2 rounded-md border border-white/10 bg-sky-900/30 px-3 py-2" />}
        </div>
      </div>
    </div>
  );
}
