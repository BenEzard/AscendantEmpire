import DesignProperties from "./DesignProperties";
import UnitDesignHeader from "./UnitDesignHeader";
import { useEffect, useMemo, useRef, useState } from "react";
import SlotPlaceHolder from "./SlotPlaceHolder";
import ComponentSelectionWindow from "./ComponentSelectionWindow";

import { DUMMY_UNIT_COMPONENTS } from "./dummyUnitComponents";
import { ALL_DAMAGE_TYPES } from "./types/UnitComponent";
import type { BattleStatsData } from "./BattleStats";
import type { BaseSlotType, ComponentCost, ComponentUpkeep, UnitComponent } from "./types/UnitComponent";
import type { UnitComponentType } from "./types/UnitComponentType";
import type { DamageType } from "./types/DamageType";

function makeInitialSlotPlaceholders() {
  return [
    {
      componentId: 1,
      type: "base" as const,
      label: "Base",
      offsetX: 0,
      offsetY: 0,
      selectedComponentId: null as number | null,
      selectedName: null as string | null,
    },
  ];
}

export interface UnitDesignerProps {
  onClose?: () => void;
}

export default function UnitDesigner({ onClose }: UnitDesignerProps) {
  const [isVisible, setIsVisible] = useState(true);
  const surfaceRef = useRef<HTMLDivElement | null>(null);

  const [surfaceSize, setSurfaceSize] = useState<{ width: number; height: number }>({ width: 0, height: 0 });

  const [slotPlaceholders, setSlotPlaceholders] = useState<
    Array<{
      componentId: number;
      type: UnitComponentType;
      label: string;
      /** Normalized offsets from the base center (0,0). */
      offsetX: number;
      offsetY: number;
      selectedComponentId?: number | null;
      selectedName?: string | null;
    }>
  >([]);
  const [selectionOpenFor, setSelectionOpenFor] = useState<null | { componentId: number; type: UnitComponentType }>(null);

  if (!isVisible) return null;

  const handleClose = () => {
    setIsVisible(false);
    onClose?.();
  };

  const resetToInitialView = () => {
    setSelectionOpenFor(null);
    setSlotPlaceholders(makeInitialSlotPlaceholders());
  };

  useEffect(() => {
    if (slotPlaceholders.length > 0) return;
    setSlotPlaceholders(makeInitialSlotPlaceholders());
  }, [slotPlaceholders.length]);

  useEffect(() => {
    const el = surfaceRef.current;
    if (!el) return;

    const update = () => {
      const r = el.getBoundingClientRect();
      setSurfaceSize({ width: r.width, height: r.height });
    };

    update();

    const ro = new ResizeObserver(() => update());
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  const setPlaceholdersForBase = (base: Extract<UnitComponent, { type: "base" }>) => {
    setSlotPlaceholders([
      {
        componentId: 1,
        type: "base",
        label: "Base",
        offsetX: 0,
        offsetY: 0,
        selectedComponentId: base.id,
        selectedName: base.name,
      },
      ...base.slots.map((s) => ({
        componentId: s.componentId,
        type: s.type,
        label: s.label,
        offsetX: s.offsetX,
        offsetY: s.offsetY,
        selectedComponentId: null,
        selectedName: null,
      })),
    ]);
  };

  const selectedComponentIdForOpenSlot =
    selectionOpenFor == null ? null : (slotPlaceholders.find((p) => p.componentId === selectionOpenFor.componentId)?.selectedComponentId ?? null);

  const selectedBaseComponent = useMemo(() => {
    const baseId = slotPlaceholders.find((p) => p.type === "base")?.selectedComponentId;
    if (baseId == null) return null;
    return (DUMMY_UNIT_COMPONENTS.find((c) => c.type === "base" && c.id === baseId) as Extract<UnitComponent, { type: "base" }> | undefined) ?? null;
  }, [slotPlaceholders]);

  const selectionComponents: UnitComponent[] = useMemo(() => {
    const slotType = selectionOpenFor?.type;
    if (!slotType) return [];

    if (slotType === "base") {
      return DUMMY_UNIT_COMPONENTS.filter((c) => c.type === "base");
    }

    // For any non-base slot, filter based on the currently selected base.
    const baseId = selectedBaseComponent?.id ?? null;
    if (baseId == null) return [];

    const asSlotType = slotType as BaseSlotType;

    return DUMMY_UNIT_COMPONENTS.filter((c) => {
      if (c.type === "base") return false;

      const slotTypes = c.slotTypes ?? [c.type];
      if (!slotTypes.includes(asSlotType)) return false;

      if (c.allowedBaseIds && !c.allowedBaseIds.includes(baseId)) return false;
      return true;
    });
  }, [selectionOpenFor?.type, selectedBaseComponent?.id]);

  const componentById = useMemo(() => {
    const m = new Map<number, UnitComponent>();
    for (const c of DUMMY_UNIT_COMPONENTS) m.set(c.id, c);
    return m;
  }, []);

  const totalDesignCost: ComponentCost = useMemo(() => {
    const totals: ComponentCost = {};

    for (const p of slotPlaceholders) {
      if (p.selectedComponentId == null) continue;
      const c = componentById.get(p.selectedComponentId);
      if (!c) continue;

      for (const [resource, amount] of Object.entries(c.common.cost)) {
        if (!amount) continue;
        totals[resource] = (totals[resource] ?? 0) + amount;
      }
    }

    return totals;
  }, [componentById, slotPlaceholders]);

  const totalDesignUpkeep: ComponentUpkeep = useMemo(() => {
    const totals: ComponentUpkeep = {};

    for (const p of slotPlaceholders) {
      if (p.selectedComponentId == null) continue;
      const c = componentById.get(p.selectedComponentId);
      if (!c) continue;

      for (const [category, resources] of Object.entries(c.common.upkeep ?? {})) {
        if (!resources) continue;
        const categoryTotals = (totals[category as keyof ComponentUpkeep] ?? {}) as Record<string, number>;

        for (const [resource, amount] of Object.entries(resources)) {
          if (!amount) continue;
          categoryTotals[resource] = (categoryTotals[resource] ?? 0) + amount;
        }

        totals[category as keyof ComponentUpkeep] = categoryTotals;
      }
    }

    return totals;
  }, [componentById, slotPlaceholders]);

  const totalDesignBattleStats: BattleStatsData = useMemo(() => {
    const emptyProfile = () => {
      const out = {} as Record<DamageType, number>;
      for (const dt of ALL_DAMAGE_TYPES) out[dt] = 0;
      return out;
    };

    // Sum offensive output from every fitted weapon and defensive resistance from every fitted armour.
    const attack = emptyProfile();
    const resistance = emptyProfile();

    for (const p of slotPlaceholders) {
      if (p.selectedComponentId == null) continue;
      const c = componentById.get(p.selectedComponentId);
      if (!c) continue;

      if ("weapon" in c) {
        for (const dt of ALL_DAMAGE_TYPES) {
          const a = c.weapon.attack[dt];
          if (Number.isFinite(a) && a) attack[dt] += a;
        }
      } else if ("armour" in c) {
        for (const dt of ALL_DAMAGE_TYPES) {
          const r = c.armour.resistance[dt];
          if (Number.isFinite(r) && r) resistance[dt] += r;
        }
      }
    }

    return { attack, resistance };
  }, [componentById, slotPlaceholders]);

  const baseIconUrl = selectedBaseComponent?.iconUrl;

  const baseIconNode = useMemo(() => {
    if (selectedBaseComponent?.type !== "base") return null;
    const Icon = selectedBaseComponent.icon;
    return Icon ? <Icon className="h-[70%] w-auto opacity-80 text-white/80" aria-hidden /> : null;
  }, [selectedBaseComponent]);

  const cx = surfaceSize.width / 2;
  const cy = surfaceSize.height / 2;

  // Scale normalized slot offsets into pixels based on current surface size.
  const dx = Math.min(260, Math.max(160, surfaceSize.width * 0.22));
  const dy = Math.min(180, Math.max(120, surfaceSize.height * 0.18));

  return (
    <div
      className="relative flex flex-col bg-sky-800"
      style={{
        width: "100%",
        height: "100%",
        flex: 1,
        minHeight: 0,
      }}>
      <UnitDesignHeader onClose={handleClose} onReset={resetToInitialView} />

      <div className="flex flex-1 min-h-0">
        <div className="relative flex-1" style={{ minWidth: 0, minHeight: 0 }}>
          <div ref={surfaceRef} className="absolute inset-0" style={{ minWidth: 0, minHeight: 0 }}>
            {(baseIconUrl || baseIconNode) && (
              <div className="pointer-events-none absolute inset-0 flex items-center justify-center">
                {baseIconUrl ? (
                  <img src={baseIconUrl} alt={selectedBaseComponent?.name ?? "Base"} className="h-[80%] w-auto opacity-80" draggable={false} />
                ) : (
                  baseIconNode
                )}
              </div>
            )}
            {slotPlaceholders.map((p) =>
              (() => {
                const selectedComponent = p.selectedComponentId != null ? componentById.get(p.selectedComponentId) : null;
                const selectedIconUrl = selectedComponent?.iconUrl ?? null;
                const selectedDisplayName = p.selectedName ?? selectedComponent?.name ?? null;
                const SelectedIcon = selectedComponent?.icon;
                const selectedIcon = SelectedIcon ? <SelectedIcon className="h-10 w-10 text-white/90" aria-hidden /> : null;

                return (
                  <SlotPlaceHolder
                    key={p.componentId}
                    componentId={p.componentId}
                    type={p.type}
                    text={p.selectedName ? `${p.label}: ${p.selectedName}` : p.label}
                    iconUrl={selectedIconUrl}
                    icon={selectedIcon}
                    name={selectedDisplayName}
                    x={cx + p.offsetX * dx}
                    y={cy + p.offsetY * dy}
                    onClick={(args) => setSelectionOpenFor(args)}
                  />
                );
              })(),
            )}
          </div>
        </div>

        <div className="basis-1/5 shrink-0" style={{ minHeight: 0 }}>
          <DesignProperties totalCost={totalDesignCost} totalUpkeep={totalDesignUpkeep} totalBattleStats={totalDesignBattleStats} />
        </div>
      </div>

      {selectionOpenFor != null && (
        <ComponentSelectionWindow
          open
          componentId={selectionOpenFor.componentId}
          type={selectionOpenFor.type}
          base={selectionOpenFor.type === "base" ? null : selectedBaseComponent}
          selectedComponentId={selectedComponentIdForOpenSlot}
          components={selectionComponents}
          onConfirm={({ selectedComponentId: _selectedComponentId, selectedName }) => {
            if (selectionOpenFor.type === "base") {
              if (_selectedComponentId == null) return;
              const base = DUMMY_UNIT_COMPONENTS.find((c) => c.type === "base" && c.id === _selectedComponentId) as
                | Extract<UnitComponent, { type: "base" }>
                | undefined;
              if (base) setPlaceholdersForBase(base);
            } else {
              setSlotPlaceholders((prev) =>
                prev.map((p) =>
                  p.componentId === selectionOpenFor.componentId
                    ? { ...p, selectedName: selectedName ?? null, selectedComponentId: _selectedComponentId }
                    : p,
                ),
              );
            }
          }}
          onClose={() => setSelectionOpenFor(null)}
        />
      )}
    </div>
  );
}
