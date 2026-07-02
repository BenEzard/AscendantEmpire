// ImageSection — the dialog's "Image" page: the component's DesignerImagePath plus a drag-to-place editor
// for laying its slots out on that image.
//
// Slots carry a normalized UI position (see slotImagePosition.ts): (0, 0) is the image center, ±1 the
// edges. A slot still sitting at the origin is treated as "unplaced" and appears as a draggable chip in
// the tray; dragging it onto the image sets its position from the drop location. A placed slot renders as
// a marker on the image that can be re-dragged to reposition (or double-clicked to send back to the tray).
//
// Slots themselves are still added/removed/relabelled on the Slots page — this page only moves them.
//
// Controlled component: it edits the passed-in component and emits the whole updated component via
// onChange, matching the pattern used by GeneralSection.

import { useEffect, useRef, useState } from "react";

import type { UnitComponent, UnitComponentSlot } from "../../types/UnitComponent";
import { FieldRow, inputClass } from "../componentSetupFields";
import { toNormalized, toPixel } from "./slotImagePosition";

interface ImageSectionProps {
  component: UnitComponent;
  onChange: (next: UnitComponent) => void;
}

// MIME-ish key for the drag payload — the index of the slot being dragged (as text).
const SLOT_DRAG_TYPE = "text/plain";

/** A slot still at the origin hasn't been placed on the image yet. */
function isPlaced(slot: UnitComponentSlot): boolean {
  return slot.uiXPosition !== 0 || slot.uiYPosition !== 0;
}

/** The label shown for a slot in the tray / on its marker. */
function slotLabel(slot: UnitComponentSlot, index: number): string {
  return slot.displayName || `Slot ${index + 1}`;
}

export default function ImageSection({ component, onChange }: ImageSectionProps) {
  const { designerImagePath, slots } = component;

  // Whether the browser failed to load `designerImagePath` as an image. Reset whenever the path itself
  // changes, so a freshly-typed path is given a chance to load before we hide the preview.
  const [loadFailed, setLoadFailed] = useState(false);
  useEffect(() => {
    setLoadFailed(false);
  }, [designerImagePath]);

  // The rendered image element and its on-screen size, tracked so markers can be positioned against it.
  // Measured after load (and kept fresh via a ResizeObserver) because the image renders at natural size.
  const imgRef = useRef<HTMLImageElement>(null);
  const [imgSize, setImgSize] = useState({ width: 0, height: 0 });

  const measureImage = () => {
    const img = imgRef.current;
    if (!img) return;
    setImgSize({ width: img.clientWidth, height: img.clientHeight });
  };

  useEffect(() => {
    const img = imgRef.current;
    if (!img) return;

    const observer = new ResizeObserver(() => measureImage());
    observer.observe(img);
    return () => observer.disconnect();
    // Re-attach whenever the image element itself may have changed (path swapped, preview re-shown).
  }, [designerImagePath, loadFailed]);

  const patchPath = (value: string) => {
    // Empty box means "no image" (null), matching the nullable backend column.
    onChange({ ...component, designerImagePath: value === "" ? null : value });
  };

  // Write a slot's normalized position back into the component.
  const setSlotPosition = (index: number, x: number, y: number) => {
    onChange({
      ...component,
      slots: slots.map((slot, i) => (i === index ? { ...slot, uiXPosition: x, uiYPosition: y } : slot)),
    });
  };

  // Only treat the path as "valid" once the <img> actually loads it — a non-empty box could still be a
  // broken/typo'd path, and we don't want to show a broken-image icon in the preview area.
  const showPreview = designerImagePath != null && designerImagePath !== "" && !loadFailed;

  // Turn a drag drop (from a tray chip or an existing marker) into a normalized position on the image.
  const handleDrop = (event: React.DragEvent) => {
    event.preventDefault();
    const index = Number(event.dataTransfer.getData(SLOT_DRAG_TYPE));
    const img = imgRef.current;
    if (!Number.isInteger(index) || index < 0 || index >= slots.length || !img) return;

    const rect = img.getBoundingClientRect();
    const { x, y } = toNormalized(event.clientX - rect.left, event.clientY - rect.top, {
      width: rect.width,
      height: rect.height,
    });
    setSlotPosition(index, x, y);
  };

  const handleDragStart = (event: React.DragEvent, index: number) => {
    event.dataTransfer.setData(SLOT_DRAG_TYPE, String(index));
    event.dataTransfer.effectAllowed = "move";
  };

  const unplacedSlots = slots.filter((slot) => !isPlaced(slot));

  return (
    <div className="w-full max-w-xl">
      <FieldRow label="Designer Image Path">
        <input className={inputClass} value={designerImagePath ?? ""} onChange={(e) => patchPath(e.target.value)} />
      </FieldRow>

      {slots.length > 0 && (
        // The tray: slots not yet placed on the image. Drag one onto the image to position it. Placed slots
        // drop out of the tray and appear as markers on the image instead.
        <div className="mb-2">
          <div className="mb-1 text-xs font-medium text-slate-500">
            {showPreview ? "Drag a slot onto the image to place it" : "Slots"}
          </div>
          {unplacedSlots.length === 0 ? (
            <p className="text-xs text-slate-400">All slots are placed. Drag a marker to move it, or double-click it to return it here.</p>
          ) : (
            <div className="flex flex-wrap gap-2">
              {slots.map((slot, index) =>
                isPlaced(slot) ? null : (
                  <span
                    key={index}
                    // Only draggable once there's an image to drop onto; otherwise it's just a label.
                    draggable={showPreview}
                    onDragStart={(e) => handleDragStart(e, index)}
                    className={`rounded-full bg-slate-100 px-2.5 py-1 text-sm text-slate-700 ${showPreview ? "cursor-grab active:cursor-grabbing" : ""}`}>
                    {slotLabel(slot, index)}
                  </span>
                ),
              )}
            </div>
          )}
        </div>
      )}

      {showPreview && (
        // Fixed-height scroll area so a large image doesn't blow out the dialog's fixed panel size; the
        // image itself renders at its natural size (no shrinking to fit), with scrollbars whenever it
        // overflows. The relative wrapper is the positioning context for the slot markers.
        <div className="mt-2 max-h-72 overflow-auto rounded-md border border-slate-300">
          <div className="relative inline-block" onDragOver={(e) => e.preventDefault()} onDrop={handleDrop}>
            <img
              ref={imgRef}
              src={designerImagePath}
              alt="Designer preview"
              className="block max-w-none"
              draggable={false}
              onLoad={measureImage}
              onError={() => setLoadFailed(true)}
            />
            {slots.map((slot, index) => {
              if (!isPlaced(slot)) return null;
              const { x, y } = toPixel(slot.uiXPosition, slot.uiYPosition, imgSize);
              return (
                <span
                  key={index}
                  draggable
                  onDragStart={(e) => handleDragStart(e, index)}
                  // Double-click sends the slot back to the tray (origin = unplaced).
                  onDoubleClick={() => setSlotPosition(index, 0, 0)}
                  title="Drag to move · double-click to unplace"
                  className="absolute -translate-x-1/2 -translate-y-1/2 cursor-grab whitespace-nowrap rounded-full border border-sky-700 bg-sky-600/90 px-2 py-0.5 text-xs font-medium text-white shadow active:cursor-grabbing"
                  style={{ left: x, top: y }}>
                  {slotLabel(slot, index)}
                </span>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}
