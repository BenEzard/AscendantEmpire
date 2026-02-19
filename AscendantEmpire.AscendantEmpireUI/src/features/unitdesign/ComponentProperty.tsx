import type { DamageType, UnitComponent } from "./types/UnitComponent";
import { ALL_DAMAGE_TYPES, labelForUnitComponentType } from "./types/UnitComponent";

export interface ComponentPropertyProps {
  selectedComponent?: UnitComponent | null;
}

function DamageRow({ label, values }: { label: string; values: Record<DamageType, number> }) {
  return (
    <div className="flex items-center justify-between gap-3">
      <div className="text-slate-600">{label}</div>
      <div className="flex items-center gap-2 font-mono text-slate-900">
        {ALL_DAMAGE_TYPES.map((dt) => (
          <div key={dt} className="min-w-12 text-right">
            {Number.isFinite(values[dt]) ? values[dt].toFixed(2) : String(values[dt])}
          </div>
        ))}
      </div>
    </div>
  );
}

export default function ComponentProperty({ selectedComponent }: ComponentPropertyProps) {
  return (
    <div className="flex h-full w-full flex-col border-l border-slate-200">
      <div className="border-b border-slate-200 px-4 py-3 text-sm font-semibold">Properties</div>
      <div className="flex-1 overflow-auto px-4 py-3 text-sm text-slate-700">
        {selectedComponent ? (
          <div className="space-y-3">
            <div>
              <div className="text-xs text-slate-500">Selected</div>
              <div className="font-semibold text-slate-900">{selectedComponent.name}</div>
              <div className="text-xs text-slate-500">{labelForUnitComponentType(selectedComponent.type)}</div>
            </div>

            <div className="space-y-1">
              <div className="flex items-center justify-between gap-3">
                <div className="text-slate-600">Mass</div>
                <div className="font-mono text-slate-900">{selectedComponent.common.mass.toFixed(2)}</div>
              </div>
              <div className="flex items-center justify-between gap-3">
                <div className="text-slate-600">Cost</div>
                <div className="font-mono text-slate-900">{selectedComponent.common.cost}</div>
              </div>
            </div>

            <div className="space-y-1">
              {Object.entries(selectedComponent.properties).map(([k, v]) => (
                <div key={k} className="flex items-center justify-between gap-3">
                  <div className="text-slate-600">{k}</div>
                  <div className="font-mono text-slate-900">{Number.isFinite(v) ? v.toFixed(2) : String(v)}</div>
                </div>
              ))}
            </div>

            {selectedComponent.type === "weapon" && (
              <div className="space-y-2">
                <div className="text-xs font-semibold text-slate-700">Weapon Damage</div>
                <div className="flex items-center justify-end gap-2 text-[11px] text-slate-500 font-mono">
                  {ALL_DAMAGE_TYPES.map((dt) => (
                    <div key={dt} className="min-w-12 text-right">
                      {dt}
                    </div>
                  ))}
                </div>
                <DamageRow label="Attack" values={selectedComponent.weapon.attack} />
                <DamageRow label="Defense" values={selectedComponent.weapon.defense} />
              </div>
            )}
          </div>
        ) : (
          <div className="text-slate-500">No component selected</div>
        )}
      </div>
    </div>
  );
}
