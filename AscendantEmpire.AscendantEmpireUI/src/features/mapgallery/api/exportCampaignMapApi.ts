import { http } from "@/api/http";

// Status polling lives in the process-agnostic processing feature:
// see src/features/processing/api/processStatusApi.ts (getProcessStatus).

/** POST /map-gallery/{id}/export — returns the processId (bare int, HTTP 201). */
export async function postInitiateMapExport(id: number, signal?: AbortSignal): Promise<number> {
  const { data } = await http.post<number>(`/map-gallery/${id}/export`, undefined, { signal });
  return data;
}

/** GET /map-gallery/{id}/export/download?processId={n} — downloads the completed export zip. */
export async function getMapExportDownload(
  id: number,
  processId: number,
  signal?: AbortSignal,
): Promise<{ blob: Blob; fileName: string }> {
  const response = await http.get(`/map-gallery/${id}/export/download`, {
    params: { processId },
    responseType: "blob",
    signal,
  });

  // Extract filename from Content-Disposition header, e.g. attachment; filename="export_MyMap_42.zip"
  const disposition: string = response.headers["content-disposition"] ?? "";
  const match = disposition.match(/filename="?([^";\n]+)"?/i);
  const fileName = match?.[1]?.trim() ?? `map-${id}-export.zip`;

  return { blob: response.data as Blob, fileName };
}
