// Converts a listed unit template (TemplateSummary, from the "load a template" combo box) back into the
// UnitDesign shape UnitDesigner edits (a base code + fitted component code per base slot).
//
// A template's fitted components are keyed by `unitComponentSlotId`, the base body's slot's database id;
// UnitDesigner only tracks slots positionally (see model/unitDesign.ts), so this walks the base body's
// `slots` in order and looks up each one's fitted code by matching `slot.id`.

import type { TemplateSummary } from "../api/templatesApi";
import type { UnitComponent } from "../types/UnitComponent";
import type { UnitDesign } from "./unitDesign";

/**
 * Resolves a template against the currently-loaded catalogue. Returns null when the template's base
 * body isn't found in the catalogue (e.g. it was removed after the template was saved), so the caller
 * can surface an error instead of loading a broken design.
 */
export function resolveTemplateDesign(template: TemplateSummary, catalog: UnitComponent[]): UnitDesign | null {
  const baseComponent = catalog.find((component) => component.code === template.bodyComponentCode);
  if (baseComponent == null) {
    return null;
  }

  // Each fitted component sits directly in one of the base body's slots, keyed by slot id.
  const slotComponentCodes = (baseComponent.body?.slots ?? []).map((slot) => {
    const fitted = template.components.find((component) => component.unitComponentSlotId === slot.id);
    return fitted?.componentCode ?? null;
  });

  return { baseCode: baseComponent.code, slotComponentCodes };
}
