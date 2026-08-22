import DesignProperties from "./DesignProperties";
import UnitDesignHeader from "./UnitDesignHeader";
import { useEffect, useMemo, useRef, useState } from "react";
import { useMutation } from "@tanstack/react-query";
import SlotPlaceHolder from "./SlotPlaceHolder";
import ComponentSelectionWindow from "./ComponentSelectionWindow";
import Modal from "@/components/ui/Modal";
import Button from "@/components/ui/Button";
import Alert from "@/components/ui/Alert";

import type { UnitComponent } from "./types/UnitComponent";
import { useUnitDesignCatalogue } from "./api/useUnitDesignCatalogue";
import {
  createGlobalUnitTemplate,
  createUnitTemplate,
  getGlobalTemplates,
  getPlayerTemplates,
  type SaveGlobalUnitTemplatePayload,
  type SaveUnitTemplatePayload,
  type TemplateSummary,
} from "./api/templatesApi";
import { componentSatisfiesSlot } from "./model/slotRules";
import { aggregateDesign, type UnitDesign } from "./model/unitDesign";
import { resolveTemplateDesign } from "./model/templateToDesign";
import { toPixel, type ImageBox } from "./model/slotImagePosition";

/** Sentinel slotIndex used for the central base slot (the base body itself). */
const BASE_SLOT_INDEX = -1;

/**
 * Derives a stable catalogue code from a display name (dev/global save flow only, where a Code is required
 * but the header's naming textbox only collects one field): lowercase, non-alphanumerics collapsed to a
 * single underscore, leading/trailing underscores trimmed.
 */
function slugify(displayName: string): string {
  return displayName
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "_")
    .replace(/^_+|_+$/g, "");
}

export interface UnitDesignerProps {
  /** "edit" (default) allows changing the base and fitted components; "view" is read-only. */
  mode?: "view" | "edit";
  /**
   * Fully-hydrated component catalogue. When omitted, the designer self-loads it from the API — hosts
   * only need to inject this for tests or when they already hold the catalogue.
   */
  catalogue?: UnitComponent[];
  /** An existing design to open. Defaults to empty (nothing chosen). */
  design?: UnitDesign;
  /**
   * When provided (and mode is "edit"), a Save button is shown that persists the assembled design via the
   * backend, then reports the resulting success message.
   */
  onSaveSuccess?: (message: string) => void;
  /** Called with a user-facing message when the save request fails. */
  onSaveError?: (message: string) => void;
  /**
   * Marks the designer as hosted by the dev Entity Setup Tool rather than by the game. It switches every
   * global/common-catalogue behaviour on at once: Save persists a shared catalogue template instead of a
   * player-owned custom one (no game/player token required, with a Code derived from the display name
   * since the header's naming textbox only collects one field), the "Load template…" combo box lists the
   * master catalogue, and the developer-only fields appear — currently just the template's map sprite.
   */
  developerMode?: boolean;
  onClose?: () => void;
}

export default function UnitDesigner({
  mode = "edit",
  catalogue: injectedCatalogue,
  design,
  onSaveSuccess,
  onSaveError,
  developerMode = false,
  onClose,
}: UnitDesignerProps) {
  const editable = mode === "edit";

  // Source the catalogue from the host when injected, otherwise self-load it.
  const selfLoad = injectedCatalogue === undefined;
  const { catalogue: loadedCatalogue, loading, error } = useUnitDesignCatalogue(selfLoad);
  const catalogue = injectedCatalogue ?? loadedCatalogue;

  const [isVisible, setIsVisible] = useState(true);

  // The assembled design: the chosen base code + the fitted component code per base slot (by index).
  const [baseCode, setBaseCode] = useState<string | null>(design?.baseCode ?? null);
  const [slotComponentCodes, setSlotComponentCodes] = useState<(string | null)[]>(design?.slotComponentCodes ?? []);

  // Which slot's selection window is open (null = closed). BASE_SLOT_INDEX targets the base itself.
  const [selectionOpenFor, setSelectionOpenFor] = useState<null | { slotIndex: number }>(null);

  // The templates offered by the header's "Load template…" combo box: the master/global catalogue for
  // the dev tool (developerMode), or the current game/player's available templates otherwise — mirroring
  // the same flag that already picks the Save endpoint. Fetched once on mount, edit mode only (a
  // read-only viewer shouldn't be able to swap out the design it was given).
  const [templates, setTemplates] = useState<TemplateSummary[]>([]);
  const [templatesLoading, setTemplatesLoading] = useState(editable);
  const [templatesError, setTemplatesError] = useState<string | null>(null);

  // Set when a template is picked while a design is already in progress, so a confirmation can be shown
  // before it overwrites the current base/slots; null when no confirmation is pending.
  const [pendingTemplate, setPendingTemplate] = useState<TemplateSummary | null>(null);

  // The template's map sprite path — the image representing this unit on the campaign map. Developer-only
  // (see the developerMode prop); players never set it.
  //
  // It lives on the template rather than being derived from the chosen base, because the sprite depends on
  // the whole assembly: a spearman and a swordsman are both built on the human body but have to read
  // differently on the map. Seeded when a template is loaded so an existing one shows its current sprite,
  // and cleared by Reset along with the rest of the design.
  const [mapImagePath, setMapImagePath] = useState("");

  useEffect(() => {
    if (!editable) {
      return;
    }

    let cancelled = false;
    setTemplatesLoading(true);
    setTemplatesError(null);

    const load = developerMode ? getGlobalTemplates() : getPlayerTemplates();
    load
      .then((result) => {
        if (!cancelled) {
          setTemplates(result);
        }
      })
      .catch((err) => {
        if (!cancelled) {
          setTemplatesError(err instanceof Error ? err.message : "Failed to load templates.");
        }
      })
      .finally(() => {
        if (!cancelled) {
          setTemplatesLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [editable, developerMode]);

  // The area available for the design canvas — used only to size the placeholder square when no base
  // image is chosen yet, and to cap how large a chosen image renders.
  const canvasAreaRef = useRef<HTMLDivElement | null>(null);
  const [canvasSize, setCanvasSize] = useState<{ width: number; height: number }>({ width: 0, height: 0 });

  // The actual rendered <img> element and its measured box. Slot uiXPosition/uiYPosition are normalized
  // against the image's own rendered box (see model/slotImagePosition.ts) — the same reference frame
  // ComponentSetupDialog's Image tab measures via its own imgRef/clientWidth/clientHeight. Measuring the
  // wrapper div instead (as this used to) diverges whenever object-fit letterboxes the image within a
  // differently-shaped box, silently shifting every slot off its authored position.
  const imgRef = useRef<HTMLImageElement | null>(null);
  const [imgBox, setImgBox] = useState<ImageBox>({ width: 0, height: 0 });

  const componentByCode = useMemo(() => {
    const map = new Map<string, UnitComponent>();
    for (const component of catalogue) {
      map.set(component.code, component);
    }
    return map;
  }, [catalogue]);

  const baseComponent = useMemo(() => (baseCode != null ? (componentByCode.get(baseCode) ?? null) : null), [baseCode, componentByCode]);

  // Slots exposed by the chosen base body (empty until a base is picked). Memoized so it has a stable
  // reference across renders, matching baseComponent's own identity.
  const baseSlots = useMemo(() => baseComponent?.body?.slots ?? [], [baseComponent]);

  // Keep the slot-assignment array length in step with the current base's slot count.
  useEffect(() => {
    setSlotComponentCodes((prev) => {
      if (prev.length === baseSlots.length) {
        return prev;
      }
      const next = baseSlots.map((_, index) => prev[index] ?? null);
      return next;
    });
  }, [baseSlots]);

  // Measure the available canvas area, used to size the no-image placeholder and cap a chosen image.
  useEffect(() => {
    const element = canvasAreaRef.current;
    if (!element) {
      return;
    }

    const update = () => {
      const rect = element.getBoundingClientRect();
      setCanvasSize({ width: rect.width, height: rect.height });
    };

    update();
    const observer = new ResizeObserver(() => update());
    observer.observe(element);
    return () => observer.disconnect();
  }, []);

  // Measure the actual <img> element's rendered box (mirrors ComponentSetupDialog's ImageSection).
  const measureImg = () => {
    const img = imgRef.current;
    if (!img) {
      return;
    }
    setImgBox({ width: img.clientWidth, height: img.clientHeight });
  };

  useEffect(() => {
    const img = imgRef.current;
    if (!img) {
      return;
    }

    const observer = new ResizeObserver(() => measureImg());
    observer.observe(img);
    return () => observer.disconnect();
    // Re-attach whenever the base (and so its image src) changes.
  }, [baseComponent?.designerImagePath]);

  const handleClose = () => {
    setIsVisible(false);
    onClose?.();
  };

  const resetDesign = () => {
    setSelectionOpenFor(null);
    setBaseCode(null);
    setSlotComponentCodes([]);
    setMapImagePath("");
  };

  // Loads a picked template's base + fitted components straight into the design, replacing whatever is
  // currently assembled.
  const applyTemplate = (template: TemplateSummary) => {
    const resolved = resolveTemplateDesign(template, catalogue);
    if (resolved == null) {
      onSaveError?.(`Could not load "${template.displayName}": its base component is missing from the catalogue.`);
      return;
    }

    setSelectionOpenFor(null);
    setBaseCode(resolved.baseCode);
    setSlotComponentCodes(resolved.slotComponentCodes);
    // Carry the saved sprite across so a loaded template shows what it currently renders as on the map.
    setMapImagePath(template.mapImagePath ?? "");
  };

  // A template is applied immediately when nothing is assembled yet; otherwise confirmation is required
  // first, since loading would silently discard the in-progress design.
  const handleSelectTemplate = (template: TemplateSummary) => {
    if (baseCode != null) {
      setPendingTemplate(template);
      return;
    }
    applyTemplate(template);
  };

  const confirmPendingTemplate = () => {
    if (pendingTemplate) {
      applyTemplate(pendingTemplate);
    }
    setPendingTemplate(null);
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
      return catalogue.filter((component) => component.componentType === "Body");
    }
    const slot = baseSlots[selectionOpenFor.slotIndex];
    if (!slot) {
      return [];
    }
    return catalogue.filter((component) => componentSatisfiesSlot(component, slot));
  }, [selectionOpenFor, catalogue, baseSlots]);

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
      setSlotComponentCodes((nextBase?.body?.slots ?? []).map(() => null));
      return;
    }

    setSlotComponentCodes((prev) => prev.map((code, index) => (index === selectionOpenFor.slotIndex ? selectedCode : code)));
  };

  const saveMutation = useMutation({
    mutationFn: (payload: SaveUnitTemplatePayload) => createUnitTemplate(payload),
  });

  const saveGlobalMutation = useMutation({
    mutationFn: (payload: SaveGlobalUnitTemplatePayload) => createGlobalUnitTemplate(payload),
  });

  // All hooks must run above this point — bailing out earlier for isVisible would call them conditionally.
  if (!isVisible) {
    return null;
  }

  const handleSave = onSaveSuccess
    ? (displayName: string) => {
        if (baseCode == null || baseComponent == null) {
          return;
        }

        // The developer picks the sprite explicitly in the properties panel. The in-game player flow has no
        // such field, so it still falls back to the base's designer image purely to satisfy the server's
        // non-empty MapImagePath check (UnitTemplateAssembly rejects a blank one). That fallback does not
        // actually render: designer images are served from public/, while the map resolves sprites out of
        // the bundled src/assets/units set, so a player-designed unit ends up with no map image. Giving
        // players a real sprite is its own piece of work — this preserves the existing behaviour rather
        // than regressing it.
        const savedMapImagePath = developerMode ? mapImagePath.trim() : (baseComponent.designerImagePath ?? "");

        // The designer tracks fittings positionally because that array is index-aligned with the `baseSlots`
        // it just rendered. The wire contract names each slot by its code instead, so that adding, removing
        // or reordering the body's slots can never silently re-map which component sits where. Empty slots
        // are simply left out of the map.
        const slotComponentCodeMap: Record<string, string> = Object.fromEntries(
          baseSlots.flatMap((slot, index) => {
            const fittedCode = slotComponentCodes[index];
            return fittedCode != null ? [[slot.code, fittedCode] as const] : [];
          }),
        );

        if (developerMode) {
          const payload: SaveGlobalUnitTemplatePayload = {
            code: slugify(displayName),
            displayName,
            mapImagePath: savedMapImagePath,
            baseCode,
            slotComponentCodes: slotComponentCodeMap,
          };

          saveGlobalMutation.mutate(payload, {
            onSuccess: (template) => onSaveSuccess(`Saved "${template.displayName}" (${template.code}).`),
            onError: (error) => onSaveError?.(error instanceof Error ? error.message : "Could not save the template."),
          });
          return;
        }

        const payload: SaveUnitTemplatePayload = {
          displayName,
          mapImagePath: savedMapImagePath,
          baseCode,
          slotComponentCodes: slotComponentCodeMap,
        };

        saveMutation.mutate(payload, {
          onSuccess: (template) => onSaveSuccess(`Saved "${template.displayName}".`),
          onError: (error) => onSaveError?.(error instanceof Error ? error.message : "Could not save the template."),
        });
      }
    : undefined;

  const hasImage = baseComponent?.designerImagePath != null;

  // A square placeholder box for when no base image is chosen yet, so slots still have a frame to sit in.
  const fallbackSize = Math.max(0, Math.min(canvasSize.width, canvasSize.height) * 0.7);

  // The box slot positions are projected against: the actual rendered <img> box when one is shown
  // (matching ComponentSetupDialog's Image tab), else the fallback placeholder square.
  const referenceBox: ImageBox = hasImage ? imgBox : { width: fallbackSize, height: fallbackSize };

  // Half-extents used to project a slot's normalized (uiX, uiY) into pixels within the reference box.
  const halfWidth = referenceBox.width / 2;
  const halfHeight = referenceBox.height / 2;
  const basePixel = toPixel(0, 0, referenceBox);

  return (
    <div
      className="relative flex flex-col bg-sky-800"
      style={{ width: "100%", height: "100%", flex: 1, minHeight: 0 }}>
      <UnitDesignHeader
        onClose={handleClose}
        onReset={resetDesign}
        onSave={editable ? handleSave : undefined}
        // In the dev tool the sprite is a required part of the template, so Save stays disabled until one
        // is entered — the server rejects a blank MapImagePath, and failing here is clearer than a 400.
        canSave={baseCode != null && (!developerMode || mapImagePath.trim() !== "")}
        defaultDisplayName={baseComponent?.displayName ?? ""}
        saving={saveMutation.isPending || saveGlobalMutation.isPending}
        onSelectTemplate={editable ? handleSelectTemplate : undefined}
        templates={templates}
        templatesLoading={templatesLoading}
        templatesError={templatesError}
      />

      <div className="flex flex-1 min-h-0">
        <div className="relative flex-1" style={{ minWidth: 0, minHeight: 0 }}>
          {/* Loading / error states for the self-loaded catalogue. */}
          {selfLoad && loading && (
            <Alert variant="info" className="absolute bottom-4 right-4 z-10 shadow-sm">
              Loading catalogue…
            </Alert>
          )}
          {selfLoad && !loading && error && <div className="absolute inset-0 flex items-center justify-center px-6 text-center text-red-300">{error}</div>}

          {/* The design canvas: a centered wrapper holding the base image and slot placeholders. The
              wrapper is sized to its content (inline-block) rather than a fixed box, so it always
              coincides exactly with the rendered <img>'s own box — the same reference frame slot
              positions were authored against in ComponentSetupDialog's Image tab. */}
          <div ref={canvasAreaRef} className="absolute inset-0 flex items-center justify-center">
            <div className="relative inline-block" style={!hasImage ? { width: fallbackSize, height: fallbackSize } : undefined}>
              {hasImage && (
                <img
                  ref={imgRef}
                  src={baseComponent!.designerImagePath!}
                  alt={baseComponent!.displayName}
                  className="pointer-events-none block opacity-80"
                  style={{
                    maxHeight: canvasSize.height ? canvasSize.height * 0.85 : undefined,
                    maxWidth: canvasSize.width ? canvasSize.width * 0.85 : undefined,
                  }}
                  draggable={false}
                  onLoad={measureImg}
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
          <DesignProperties
            aggregate={aggregate}
            mapImagePath={mapImagePath}
            // Supplying the change handler is what reveals the developer-only Map Image card; the in-game
            // host leaves it undefined, so players never see the field at all.
            onMapImagePathChange={developerMode ? setMapImagePath : undefined}
          />
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

      <Modal
        open={pendingTemplate != null}
        title="Replace current design?"
        onClose={() => setPendingTemplate(null)}
        footer={
          <>
            <Button variant="light" onClick={() => setPendingTemplate(null)}>
              Cancel
            </Button>
            <Button variant="primary" onClick={confirmPendingTemplate}>
              Load Template
            </Button>
          </>
        }>
        Loading &ldquo;{pendingTemplate?.displayName}&rdquo; will discard the design currently assembled here.
      </Modal>
    </div>
  );
}
