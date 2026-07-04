import type { UnitComponent } from "./types/UnitComponent";
import type { BattleStatsData } from "./BattleStats";
import { BattleStats } from "./BattleStats";
import { ALL_DAMAGE_TYPES, rawToScaled } from "./types/UnitComponent";
import { UpkeepStats } from "./UpkeepStats";
import { TrainingCostStats } from "./TrainingCostStats";
// PLACEHOLDER_COST_UPKEEP: cost/upkeep are not modelled server-side yet — sourced from stand-ins.
import { placeholderCostFor, placeholderUpkeepFor } from "./model/placeholderCosts";

export interface ComponentPropertyProps {
  selectedComponent?: UnitComponent | null;
  existingSelection?: UnitComponent | null;
}

// Build the BattleStats data from whichever combat extensions a component carries: a weapon contributes
// attack (+ range/domain modifiers), armour contributes resistance. A Body may carry either inherently.
function toBattleStats(component?: UnitComponent | null): BattleStatsData | undefined {
  if (!component) {
    return undefined;
  }

  const stats: BattleStatsData = {};
  if (component.weapon) {
    stats.attack = component.weapon.attack;
    stats.minimumRange = component.weapon.minimumRange;
    stats.maximumRange = component.weapon.maximumRange;
    stats.domainModifiers = component.weapon.domainModifiers;
  }
  if (component.armour) {
    stats.resistance = component.armour.resistance;
  }

  return stats.attack || stats.resistance ? stats : undefined;
}

/** Renders a labelled Scale-1,000 stat row, or nothing when the raw value is null/absent. */
function StatRow({ label, raw }: { label: string; raw: number | null | undefined }) {
  if (raw == null) {
    return null;
  }

  return (
    <div className="flex items-center justify-between gap-3">
      <div className="text-white/80">{label}</div>
      <div className="font-mono tabular-nums text-white">{rawToScaled(raw).toFixed(2)}</div>
    </div>
  );
}

export default function ComponentProperty({ selectedComponent, existingSelection }: ComponentPropertyProps) {
  const battleStats = toBattleStats(selectedComponent);
  const existingBattleStats = toBattleStats(existingSelection);

  // Only show the battle panel when there is some non-zero damage value to display (current or existing).
  const shouldShowBattleStats =
    battleStats != null &&
    ALL_DAMAGE_TYPES.some((dt) => {
      const values = [
        battleStats.attack?.[dt],
        battleStats.resistance?.[dt],
        existingBattleStats?.attack?.[dt],
        existingBattleStats?.resistance?.[dt],
      ];
      return values.some((v) => v != null && Number.isFinite(v) && v > 0);
    });

  return (
    <div className="flex h-full w-full flex-col border-l border-white/10 bg-sky-900/20">
      <div className="border-b border-white/10 px-4 py-3 text-sm font-semibold text-white">Properties</div>
      <div className="flex-1 overflow-auto px-4 py-3 text-sm text-white">
        {selectedComponent ? (
          <div className="space-y-3">
            <div className="space-y-1">
              <div className="text-base font-semibold text-white">{selectedComponent.displayName}</div>
              <StatRow label="Weight" raw={selectedComponent.weight} />
              <StatRow label="Detection" raw={selectedComponent.detection} />
              <StatRow label="Visibility" raw={selectedComponent.visibility} />
            </div>

            {/* PLACEHOLDER_COST_UPKEEP: stand-in cost/upkeep until the backend models them. */}
            <TrainingCostStats cost={placeholderCostFor(selectedComponent)} />

            <UpkeepStats
              upkeep={placeholderUpkeepFor(selectedComponent)}
              existingUpkeep={existingSelection ? placeholderUpkeepFor(existingSelection) : undefined}
            />

            {shouldShowBattleStats && battleStats && <BattleStats stats={battleStats} existingStats={existingBattleStats} />}
          </div>
        ) : (
          <div className="text-white/70">No component selected</div>
        )}
      </div>
    </div>
  );
}
