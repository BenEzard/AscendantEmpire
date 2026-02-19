import DesignProperties from "./DesignProperties";
import UnitDesignHeader from "./UnitDesignHeader";
import { useEffect, useMemo, useRef, useState } from "react";
import SlotPlaceHolder from "./SlotPlaceHolder";
import ComponentSelectionWindow from "./ComponentSelectionWindow";

import { DUMMY_UNIT_COMPONENTS } from "./dummyUnitComponents";
import type { BaseSlotType, UnitComponent } from "./types/UnitComponent";
import type { UnitComponentType } from "./types/UnitComponentType";

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

  useEffect(() => {
    if (slotPlaceholders.length > 0) return;
    setSlotPlaceholders([
      {
        componentId: 1,
        type: "base",
        label: "Base",
        offsetX: 0,
        offsetY: 0,
        selectedComponentId: null,
        selectedName: null,
      },
    ]);
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

  const baseIconUrl = selectedBaseComponent?.iconUrl;

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
      <UnitDesignHeader onClose={handleClose} />

      <div className="flex flex-1 min-h-0">
        <div className="relative flex-1" style={{ minWidth: 0, minHeight: 0 }}>
          <div ref={surfaceRef} className="absolute inset-0" style={{ minWidth: 0, minHeight: 0 }}>
            {baseIconUrl && (
              <div className="pointer-events-none absolute inset-0 flex items-center justify-center">
                <img src={baseIconUrl} alt={selectedBaseComponent?.name ?? "Base"} className="h-[80%] w-auto opacity-80" draggable={false} />
              </div>
            )}
            {slotPlaceholders.map((p) => (
              (() => {
                const selectedComponent = p.selectedComponentId != null ? componentById.get(p.selectedComponentId) : null;
                const selectedIconUrl = selectedComponent?.iconUrl ?? null;
                const selectedDisplayName = p.selectedName ?? selectedComponent?.name ?? null;

                return (
              <SlotPlaceHolder
                key={p.componentId}
                componentId={p.componentId}
                type={p.type}
                text={p.selectedName ? `${p.label}: ${p.selectedName}` : p.label}
                iconUrl={selectedIconUrl}
                name={selectedDisplayName}
                x={cx + p.offsetX * dx}
                y={cy + p.offsetY * dy}
                onClick={(args) => setSelectionOpenFor(args)}
              />
                );
              })()
            ))}
          </div>
        </div>

        <div className="basis-1/5 shrink-0" style={{ minHeight: 0 }}>
          <DesignProperties />
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
              const base = DUMMY_UNIT_COMPONENTS.find((c) => c.type === "base" && c.id === _selectedComponentId) as
                | Extract<UnitComponent, { type: "base" }>
                | undefined;
              if (base) setPlaceholdersForBase(base);
            } else {
              setSlotPlaceholders((prev) =>
                prev.map((p) =>
                  p.componentId === selectionOpenFor.componentId ? { ...p, selectedName, selectedComponentId: _selectedComponentId } : p,
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
