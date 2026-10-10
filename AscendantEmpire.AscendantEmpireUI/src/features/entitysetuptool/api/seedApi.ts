import { http } from "@/api/http";

// Status polling lives in the process-agnostic processing feature:
// see src/features/processing/api/processStatusApi.ts (getProcessStatus).

/**
 * One selectable part of the database seed. 
 * Mirrors the backend SeedTarget enum, which the API serialises as its member name rather than its number.
 */
export type SeedTarget =
  | "Geographies"
  | "Biomes"
  | "MapResourceCategories"
  | "TagTypes"
  | "Tags"
  | "UnitComponents"
  | "StarterTemplates"
  | "Assets"
  | "ExperienceTypes"
  | "Technologies"
  | "MapResourceTypes";

/** The heading a target is listed under. Mirrors the backend SeedTargetGroup enum. */
export type SeedTargetGroup = "Map" | "UnitDesign" | "Assets" | "Technology";

/**
 * A seed target as the selection dialog needs it. Mirrors the backend DTSeedTarget.
 *
 * Nothing here is hard-coded in the frontend — the labels, the grouping and the dependency edges all come
 * from the registered seed steps, so a new seed target shows up in the dialog with no change on this side.
 */
export interface SeedTargetInfo {
  target: SeedTarget;
  group: SeedTargetGroup;
  displayName: string;
  description: string;
  /** Direct prerequisites only. The dialog walks them transitively, as the server does. */
  dependsOn: SeedTarget[];
  sortOrder: number;
  /** How many rows this target would write, for the count beside its label. */
  itemCount: number;
}

/**
 * GET /system/seed/targets — the parts of the database that can be seeded, already ordered the way the
 * dialog lists them (by group, then by sort order).
 */
export async function getSeedTargets(signal?: AbortSignal): Promise<SeedTargetInfo[]> {
  const { data } = await http.get<SeedTargetInfo[]>("/system/seed/targets", { signal });
  return data;
}

/**
 * POST /system/seed — initiates seeding the selected targets and returns the processId (bare int, HTTP 201)
 * so the caller can poll seed progress.
 *
 * Prerequisites need not be included: the server expands the selection over them before queueing the job.
 * The dialog ticks them too, so the user can see what they are in for, but this end is where it is enforced.
 */
export async function postInitiateDatabaseSeed(targets: SeedTarget[], signal?: AbortSignal): Promise<number> {
  const { data } = await http.post<number>("/system/seed", { targets }, { signal });
  return data;
}
