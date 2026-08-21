// API service for persisting unit templates. Thin wrapper over the shared axios `http` client for the
// create/update endpoints of two different controllers: the player-owned custom flow on
// UnitDesignerController (POST/PUT /unit-designer/templates), and the dev tool's global/common catalog
// flow on GlobalUnitTemplatesController (POST/PUT /global-unit-templates).

import { http } from "@/api/http";

/** The request body accepted by both the create and update save endpoints (mirrors SaveUnitTemplateDto). */
export interface SaveUnitTemplatePayload {
  displayName: string;
  description?: string | null;
  /** Image to show on the Map. */
  mapImagePath: string;
  /** Code of the chosen base Body component. */
  baseCode: string;
  /**
   * The component fitted into each filled slot, keyed by the base body's slot code. Only filled slots
   * appear — an absent slot code means that slot is empty.
   */
  slotComponentCodes: Record<string, string>;
}

/** Server response shape (mirrors UnitTemplateView). */
export interface UnitTemplateView {
  id: number;
  displayName: string;
  description: string | null;
  mapImagePath: string;
  bodyComponentId: number;
  bodyComponentCode: string;
}

/** Create a new custom unit template. Returns the created template (with its assigned id). */
export async function createUnitTemplate(payload: SaveUnitTemplatePayload): Promise<UnitTemplateView> {
  const { data } = await http.post<UnitTemplateView>("/unit-designer/templates", payload);
  return data;
}

/** Update an existing custom unit template by id. Returns the updated template. */
export async function updateUnitTemplate(id: number, payload: SaveUnitTemplatePayload): Promise<UnitTemplateView> {
  const { data } = await http.put<UnitTemplateView>(`/unit-designer/templates/${id}`, payload);
  return data;
}

/** The request body for the dev tool's global/common template create+update endpoints (mirrors SaveGlobalUnitTemplateDto). */
export interface SaveGlobalUnitTemplatePayload {
  /** Unique, stable key for the template, e.g. "scout_walker_mk1". */
  code: string;
  displayName: string;
  description?: string | null;
  mapImagePath: string;
  baseCode: string;
  /** The component fitted into each filled slot, keyed by the base body's slot code. */
  slotComponentCodes: Record<string, string>;
}

/** Server response shape (mirrors GlobalUnitTemplateView). */
export interface GlobalUnitTemplateView {
  id: number;
  code: string;
  displayName: string;
  description: string | null;
  mapImagePath: string;
  bodyComponentId: number;
  bodyComponentCode: string;
}

/** Dev tool: create a new global/common unit template in the shared catalog. */
export async function createGlobalUnitTemplate(payload: SaveGlobalUnitTemplatePayload): Promise<GlobalUnitTemplateView> {
  const { data } = await http.post<GlobalUnitTemplateView>("/global-unit-templates", payload);
  return data;
}

/** Dev tool: update an existing global/common unit template by id. */
export async function updateGlobalUnitTemplate(id: number, payload: SaveGlobalUnitTemplatePayload): Promise<GlobalUnitTemplateView> {
  const { data } = await http.put<GlobalUnitTemplateView>(`/unit-design/templates/global/${id}`, payload);
  return data;
}

/** Server response shape for the bulk recompilation (mirrors RecompileAllUnitTemplatesResult). */
export interface RecompileAllUnitTemplatesResult {
  /** How many templates were successfully recompiled. */
  recompiledCount: number;
  /** Per-template failure messages for templates that could not be recompiled. */
  failures: string[];
}

/**
 * Dev tool: recompile every unit template's compiled snapshot. Use after editing catalogue components,
 * whose changes don't propagate to the snapshots of templates already built with them. Best-effort per
 * template — failures come back in the result rather than failing the whole call.
 */
export async function recompileAllTemplates(): Promise<RecompileAllUnitTemplatesResult> {
  const { data } = await http.post<RecompileAllUnitTemplatesResult>("/unit-design/templates/recompile-all");
  return data;
}

// --- template listing (for the designer's "load a template" combo box) -------------------------

/** A component fitted into one of a template's body slots (mirrors the backend DesignTemplateComponent). */
export interface TemplateComponentSummary {
  id: number;
  unitComponentSlotId: number;
  unitComponentId: number;
  componentCode: string;
  componentDisplayName: string;
  componentType: string;
}

/** A unit template as listed for loading (mirrors the backend DesignTemplate). */
export interface TemplateSummary {
  id: number;
  /** Stable key for global/common templates; null for custom player-owned ones. */
  code: string | null;
  displayName: string;
  description: string | null;
  mapImagePath: string;
  /** True for the player's own custom template; false for a global/common one. */
  isCustom: boolean;
  hasBeenPrototyped: boolean;
  bodyComponentId: number;
  bodyComponentCode: string;
  bodyComponentDisplayName: string;
  components: TemplateComponentSummary[];
}

/** Wire shape shared by GET /unit-design-catalogue and GET /unit-designer/templates: only `templates` is read here. */
interface TemplatesWire {
  templates: TemplateSummary[];
}

/**
 * Dev tool: the master/global unit-template catalogue, unrelated to any game — for the Entity Setup
 * Tool's "load a template" combo box. Reads GET /unit-design-catalogue (the unfiltered dev god-view) and
 * keeps only the global/common templates (`!isCustom`), dropping the per-game custom ones it also returns.
 */
export async function getGlobalTemplates(): Promise<TemplateSummary[]> {
  const { data } = await http.get<TemplatesWire>("/unit-design-catalogue");
  return data.templates.filter((template) => !template.isCustom);
}

/**
 * The templates available to the current player in the current game — global/common templates they've
 * unlocked plus their own custom ones. Reads GET /unit-designer/templates, which is already scoped to the
 * caller's game/player by their token, so no client-side filtering is needed.
 */
export async function getPlayerTemplates(): Promise<TemplateSummary[]> {
  const { data } = await http.get<TemplatesWire>("/unit-designer/templates");
  return data.templates;
}
