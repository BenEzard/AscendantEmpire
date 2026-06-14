import { http } from "@/api/http";

// Status polling lives in the process-agnostic processing feature:
// see src/features/processing/api/processStatusApi.ts (getProcessStatus).

/**
 * POST /map-gallery/import — uploads a previously exported map zip and returns the processId
 * (bare int, HTTP 201) so the caller can poll import progress.
 */
export async function postInitiateMapImport(file: File, signal?: AbortSignal): Promise<number> {
  const formData = new FormData();
  formData.append("file", file);

  // axios sets the multipart boundary on the Content-Type header automatically from the FormData.
  const { data } = await http.post<number>("/map-gallery/import", formData, { signal });
  return data;
}
