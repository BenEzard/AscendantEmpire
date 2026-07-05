import { http } from "@/api/http";

// Status polling lives in the process-agnostic processing feature:
// see src/features/processing/api/processStatusApi.ts (getProcessStatus).

/** Which catalogue kinds an export should include. Grows as the export dialog gains more checkboxes. */
export interface CatalogExportSelection {
  components: boolean;
  templates: boolean;
}

/** POST /unit-design/export — returns the processId (bare int, HTTP 201). */
export async function postInitiateCatalogExport(
  selection: CatalogExportSelection,
  signal?: AbortSignal,
): Promise<number> {
  const { data } = await http.post<number>("/unit-design/export", selection, { signal });
  return data;
}

/** GET /unit-design/export/download?processId={n} — downloads the completed export zip. */
export async function getCatalogExportDownload(
  processId: number,
  signal?: AbortSignal,
): Promise<{ blob: Blob; fileName: string }> {
  const response = await http.get(`/unit-design/export/download`, {
    params: { processId },
    responseType: "blob",
    signal,
  });

  // Extract filename from Content-Disposition header, e.g. attachment; filename="catalogue_export_42.zip"
  const disposition: string = response.headers["content-disposition"] ?? "";
  const match = disposition.match(/filename="?([^";\n]+)"?/i);
  const fileName = match?.[1]?.trim() ?? `catalogue-export-${processId}.zip`;

  return { blob: response.data as Blob, fileName };
}

/**
 * POST /unit-design/import — uploads a previously exported catalogue zip and returns the processId
 * (bare int, HTTP 201) so the caller can poll import progress.
 */
export async function postInitiateCatalogImport(file: File, signal?: AbortSignal): Promise<number> {
  const formData = new FormData();
  formData.append("file", file);

  // axios sets the multipart boundary on the Content-Type header automatically from the FormData.
  const { data } = await http.post<number>("/unit-design/import", formData, { signal });
  return data;
}
