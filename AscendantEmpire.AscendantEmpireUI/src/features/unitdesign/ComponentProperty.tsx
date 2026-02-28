import type { UnitComponent } from "./types/UnitComponent";
import { BattleStats } from "./BattleStats";
import { ALL_DAMAGE_TYPES } from "./types/UnitComponent";
import { UpkeepStats } from "./UpkeepStats";
import { TrainingCostStats } from "./TrainingCostStats";

export interface ComponentPropertyProps {
  selectedComponent?: UnitComponent | null;
  existingSelection?: UnitComponent | null;
}

export default function ComponentProperty({ selectedComponent, existingSelection }: ComponentPropertyProps) {
  const battleStats = selectedComponent && "weapon" in selectedComponent ? selectedComponent.weapon : undefined;
  const existingBattleStats = existingSelection && "weapon" in existingSelection ? existingSelection.weapon : undefined;
  const shouldShowBattleStats =
    battleStats != null &&
    ALL_DAMAGE_TYPES.some((dt) => {
      const attack = battleStats.attack[dt];
      const defense = battleStats.defense[dt];
      const existingAttack = existingBattleStats?.attack[dt] ?? 0;
      const existingDefense = existingBattleStats?.defense[dt] ?? 0;
      return (
        (Number.isFinite(attack) && attack > 0) ||
        (Number.isFinite(defense) && defense > 0) ||
        (Number.isFinite(existingAttack) && existingAttack > 0) ||
        (Number.isFinite(existingDefense) && existingDefense > 0)
      );
    });

  return (
    <div className="flex h-full w-full flex-col border-l border-white/10 bg-sky-900/20">
      <div className="border-b border-white/10 px-4 py-3 text-sm font-semibold text-white">Properties</div>
      <div className="flex-1 overflow-auto px-4 py-3 text-sm text-white">
        {selectedComponent ? (
          <div className="space-y-3">
            <div className="space-y-1">
              <div className="flex items-center justify-between gap-3">
                <div className="text-white/80">Mass</div>
                <div className="font-mono tabular-nums text-white">{selectedComponent.common.mass.toFixed(2)}</div>
              </div>
            </div>

            <TrainingCostStats cost={selectedComponent.common.cost} />

            <UpkeepStats upkeep={selectedComponent.common.upkeep} existingUpkeep={existingSelection?.common.upkeep} />

            <div className="space-y-1">
              {Object.entries(selectedComponent.properties).map(([k, v]) => (
                <div key={k} className="flex items-center justify-between gap-3">
                  <div className="text-white/80">{k}</div>
                  <div className="font-mono tabular-nums text-white">{Number.isFinite(v) ? v.toFixed(2) : String(v)}</div>
                </div>
              ))}
            </div>

            {shouldShowBattleStats && battleStats && <BattleStats stats={battleStats} existingStats={existingBattleStats} />}
          </div>
        ) : (
          <div className="text-white/70">No component selected</div>
        )}
      </div>
    </div>
  );
}
