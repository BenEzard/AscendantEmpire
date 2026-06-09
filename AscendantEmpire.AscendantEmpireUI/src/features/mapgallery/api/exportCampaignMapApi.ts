import { http } from "@/api/http";

export interface ProcessingStatusDto {
  processingId: number;
  processType: number;
  /** 0=Pending, 1=InProgress, 2=Complete, 3=Failed */
  status: number;
  /** 0–100, updated by the background service at key processing steps. */
  progressPercent: number;
  startedAt: string;
  completedAt: string | null;
  errorMessage: string | null;
}

/** POST /map-gallery/{id}/export — returns the processId (bare int, HTTP 201). */
export async function postInitiateMapExport(id: number, signal?: AbortSignal): Promise<number> {
  const { data } = await http.post<number>(`/map-gallery/${id}/export`, undefined, { signal });
  return data;
}

/** GET /system/status?processId={n} — returns the current processing status. */
export async function getProcessingStatus(processId: number, signal?: AbortSignal): Promise<ProcessingStatusDto> {
  const { data } = await http.get<ProcessingStatusDto>("/system/status", {
    params: { processId },
    signal,
  });
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
