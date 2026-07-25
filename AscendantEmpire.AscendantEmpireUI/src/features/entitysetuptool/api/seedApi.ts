import { http } from "@/api/http";

// Status polling lives in the process-agnostic processing feature:
// see src/features/processing/api/processStatusApi.ts (getProcessStatus).

/**
 * POST /system/seed — initiates seeding the database with its initial reference/catalogue values and
 * returns the processId (bare int, HTTP 201) so the caller can poll seed progress. Takes no body; all
 * values come from the backend's static seed-data classes.
 */
export async function postInitiateDatabaseSeed(signal?: AbortSignal): Promise<number> {
  const { data } = await http.post<number>("/system/seed", undefined, { signal });
  return data;
}
