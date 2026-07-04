import DesignProperties from "./DesignProperties";
import UnitDesignHeader from "./UnitDesignHeader";
import { useEffect, useMemo, useRef, useState } from "react";
import SlotPlaceHolder from "./SlotPlaceHolder";
import ComponentSelectionWindow from "./ComponentSelectionWindow";

import type { UnitComponent } from "./types/UnitComponent";
import { useUnitDesignCatalog } from "./api/useUnitDesignCatalog";
import { componentSatisfiesSlot } from "./model/slotRules";
import { aggregateDesign, type UnitDesign } from "./model/unitDesign";
import { toPixel, type ImageBox } from "./model/slotImagePosition";

/** Sentinel slotIndex used for the central base slot (the base body itself). */
const BASE_SLOT_INDEX = -1;

export interface UnitDesignerProps {
  /** "edit" (default) allows changing the base and fitted components; "view" is read-only. */
  mode?: "view" | "edit";
  /**
   * Fully-hydrated component catalogue. When omitted, the designer self-loads it from the API — hosts
   * only need to inject this for tests or when they already hold the catalogue.
   */
  catalog?: UnitComponent[];
  /** An existing design to open. Defaults to empty (nothing chosen). */
  design?: UnitDesign;
  /** When provided (and mode is "edit"), a Save button hands the assembled design back to the host. */
  onSave?: (design: UnitDesign) => void;
  onClose?: () => void;
}

export default function UnitDesigner({ mode = "edit", catalog: injectedCatalog, design, onSave, onClose }: UnitDesignerProps) {
  const editable = mode === "edit";

  // Source the catalogue from the host when injected, otherwise self-load it.
  const selfLoad = injectedCatalog === undefined;
  const { catalog: loadedCatalog, loading, error } = useUnitDesignCatalog(selfLoad);
  const catalog = injectedCatalog ?? loadedCatalog;

  const [isVisible, setIsVisible] = useState(true);

  // The assembled design: the chosen base code + the fitted component code per base slot (by index).
  const [baseCode, setBaseCode] = useState<string | null>(design?.baseCode ?? null);
  const [slotComponentCodes, setSlotComponentCodes] = useState<(string | null)[]>(design?.slotComponentCodes ?? []);

  // Which slot's selection window is open (null = closed). BASE_SLOT_INDEX targets the base itself.
  const [selectionOpenFor, setSelectionOpenFor] = useState<null | { slotIndex: number }>(null);

  // The base image wrapper is the positioning canvas: slot offsets are normalized to its half-extent.
  const imageBoxRef = useRef<HTMLDivElement | null>(null);
  const [imageBox, setImageBox] = useState<ImageBox>({ width: 0, height: 0 });

  const componentByCode = useMemo(() => {
    const map = new Map<string, UnitComponent>();
    for (const component of catalog) {
      map.set(component.code, component);
    }
    return map;
  }, [catalog]);

  const baseComponent = useMemo(() => (baseCode != null ? (componentByCode.get(baseCode) ?? null) : null), [baseCode, componentByCode]);

  // Slots exposed by the chosen base body (empty until a base is picked).
  const baseSlots = baseComponent?.slots ?? [];

  // Keep the slot-assignment array length in step with the current base's slot count.
  useEffect(() => {
    setSlotComponentCodes((prev) => {
      if (prev.length === baseSlots.length) {
        return prev;
      }
      const next = baseSlots.map((_, index) => prev[index] ?? null);
      return next;
    });
  }, [baseSlots.length]);

  // Measure the base image wrapper so normalized slot positions can be projected to pixels.
  useEffect(() => {
    const element = imageBoxRef.current;
    if (!element) {
      return;
    }

    const update = () => {
      const rect = element.getBoundingClientRect();
      setImageBox({ width: rect.width, height: rect.height });
    };

    update();
    const observer = new ResizeObserver(() => update());
    observer.observe(element);
    return () => observer.disconnect();
  }, [baseComponent?.code]);

  if (!isVisible) {
    return null;
  }

  const handleClose = () => {
    setIsVisible(false);
    onClose?.();
  };

  const resetDesign = () => {
    setSelectionOpenFor(null);
    setBaseCode(null);
    setSlotComponentCodes([]);
  };

  // Fitted components resolved from their codes (nulls / unknown codes dropped) for aggregation.
  const fittedComponents = slotComponentCodes
    .map((code) => (code != null ? (componentByCode.get(code) ?? null) : null))
    .filter((component): component is UnitComponent => component != null);

  const aggregate = useMemo(() => aggregateDesign(baseComponent, fittedComponents), [baseComponent, fittedComponents]);

  // Candidate components offered by the open selection window: bodies for the base slot, or the
  // components a slot's acceptance rules admit for a normal slot.
  const selectionComponents: UnitComponent[] = useMemo(() => {
    if (selectionOpenFor == null) {
      return [];
    }
    if (selectionOpenFor.slotIndex === BASE_SLOT_INDEX) {
      return catalog.filter((component) => component.componentType === "Body");
    }
    const slot = baseSlots[selectionOpenFor.slotIndex];
    if (!slot) {
      return [];
    }
    return catalog.filter((component) => componentSatisfiesSlot(component, slot));
  }, [selectionOpenFor, catalog, baseSlots]);

  const openSelectionCode =
    selectionOpenFor == null
      ? null
      : selectionOpenFor.slotIndex === BASE_SLOT_INDEX
        ? baseCode
        : (slotComponentCodes[selectionOpenFor.slotIndex] ?? null);

  const openSelectionTitle =
    selectionOpenFor == null
      ? ""
      : selectionOpenFor.slotIndex === BASE_SLOT_INDEX
        ? "Base Selection"
        : (baseSlots[selectionOpenFor.slotIndex]?.displayName ?? `Slot ${selectionOpenFor.slotIndex + 1}`);

  const handleConfirmSelection = ({ selectedCode }: { selectedCode: string | null; selectedName: string | null }) => {
    if (selectionOpenFor == null) {
      return;
    }

    if (selectionOpenFor.slotIndex === BASE_SLOT_INDEX) {
      // Choosing a new base resets the slot assignments (the new base defines a different slot set).
      if (selectedCode == null) {
        return;
      }
      const nextBase = componentByCode.get(selectedCode);
      setBaseCode(selectedCode);
      setSlotComponentCodes((nextBase?.slots ?? []).map(() => null));
      return;
    }

    setSlotComponentCodes((prev) => prev.map((code, index) => (index === selectionOpenFor.slotIndex ? selectedCode : code)));
  };

  const handleSave = onSave
    ? () => onSave({ baseCode, slotComponentCodes })
    : undefined;

  // Half-extents used to project a slot's normalized (uiX, uiY) into pixels within the image wrapper.
  const halfWidth = imageBox.width / 2;
  const halfHeight = imageBox.height / 2;
  const basePixel = toPixel(0, 0, imageBox);

  return (
    <div
      className="relative flex flex-col bg-sky-800"
      style={{ width: "100%", height: "100%", flex: 1, minHeight: 0 }}>
      <UnitDesignHeader onClose={handleClose} onReset={resetDesign} onSave={editable ? handleSave : undefined} canSave={baseCode != null} />

      <div className="flex flex-1 min-h-0">
        <div className="relative flex-1" style={{ minWidth: 0, minHeight: 0 }}>
          {/* Loading / error / empty states for the self-loaded catalogue. */}
          {selfLoad && loading && <div className="absolute inset-0 flex items-center justify-center text-white/80">Loading catalogue…</div>}
          {selfLoad && !loading && error && <div className="absolute inset-0 flex items-center justify-center px-6 text-center text-red-300">{error}</div>}

          {/* The design canvas: a centered square wrapper holding the base image and slot placeholders. */}
          <div className="absolute inset-0 flex items-center justify-center">
            <div ref={imageBoxRef} className="relative h-[70%] aspect-square">
              {baseComponent?.designerImagePath && (
                <img
                  src={baseComponent.designerImagePath}
                  alt={baseComponent.displayName}
                  className="pointer-events-none absolute inset-0 h-full w-full object-contain opacity-80"
                  draggable={false}
                />
              )}

              {/* Central base slot — clicking it picks the base body. */}
              <SlotPlaceHolder
                slotIndex={BASE_SLOT_INDEX}
                componentType="Body"
                text="Base"
                name={baseComponent?.displayName ?? null}
                x={basePixel.x}
                y={basePixel.y}
                interactive={editable}
                onClick={editable ? (args) => setSelectionOpenFor(args) : undefined}
              />

              {/* One placeholder per base slot, positioned from its normalized UI offset. */}
              {baseSlots.map((slot, index) => {
                const fittedCode = slotComponentCodes[index] ?? null;
                const fitted = fittedCode != null ? componentByCode.get(fittedCode) : null;
                // The slot's first ComponentType rule gives a representative type for tinting.
                const typeRule = slot.rules.find((rule) => rule.ruleType === "ComponentType");
                const representativeType = typeRule ? (typeRule.value.split(",")[0]?.trim() as UnitComponent["componentType"]) : undefined;

                return (
                  <SlotPlaceHolder
                    key={index}
                    slotIndex={index}
                    componentType={representativeType}
                    text={slot.displayName ?? `Slot ${index + 1}`}
                    name={fitted?.displayName ?? null}
                    iconUrl={fitted?.designerImagePath ?? null}
                    x={halfWidth + slot.uiXPosition * halfWidth}
                    y={halfHeight + slot.uiYPosition * halfHeight}
                    interactive={editable}
                    onClick={editable ? (args) => setSelectionOpenFor(args) : undefined}
                  />
                );
              })}
            </div>
          </div>
        </div>

        <div className="basis-1/5 shrink-0" style={{ minHeight: 0 }}>
          <DesignProperties aggregate={aggregate} />
        </div>
      </div>

      {selectionOpenFor != null && (
        <ComponentSelectionWindow
          open
          title={openSelectionTitle}
          isBaseSlot={selectionOpenFor.slotIndex === BASE_SLOT_INDEX}
          components={selectionComponents}
          selectedCode={openSelectionCode}
          onConfirm={handleConfirmSelection}
          onClose={() => setSelectionOpenFor(null)}
        />
      )}
    </div>
  );
}
