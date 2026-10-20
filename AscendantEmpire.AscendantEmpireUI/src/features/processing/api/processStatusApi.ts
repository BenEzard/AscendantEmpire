import { http } from "@/api/http";

/**
 * The names of the backend ProcessingStatus enum, verbatim.
 *
 * Enums cross the wire as their member names rather than their numbers — the API registers JsonStringEnumConverter in
 * Program.cs — so this is a string union, as everywhere else in the app. It was a numeric type until a failed game
 * delete went unreported: every comparison against a number silently missed, leaving the poller running against a job
 * that had already finished.
 */
export type ProcessingStatusName = "Pending" | "InProgress" | "Complete" | "Failed";

/** The names of the backend ProcessType enum, carried the same way. */
export type ProcessTypeName =
  | "ExportGalleryMap"
  | "GenerateCampaignMap"
  | "RenderCampaignMapPreview"
  | "ImportGalleryMap"
  | "ExportCatalogue"
  | "ImportCatalogue"
  | "SeedDatabase"
  | "DeleteGame";

/** The statuses the server will not move a process on from, whichever way it ended. */
export const TERMINAL_PROCESSING_STATUSES: readonly ProcessingStatusName[] = ["Complete", "Failed"];

/** Mirrors the backend DTProcessingStatus returned by GET /system/status. */
export interface ProcessingStatusDto {
  processingId: number;
  processType: ProcessTypeName;
  status: ProcessingStatusName;
  /** 0–100, updated by the background processing service at key steps. */
  progressPercent: number;
  startedAt: string;
  completedAt: string | null;
  /** The account of the failure given by the server. Only populated once status is "Failed". */
  errorMessage: string | null;
}

/**
 * GET /system/status?processId={n} — returns the current status of any background process.
 * Process-agnostic: works for map export, map generation, or any future ProcessType.
 */
export async function getProcessStatus(processId: number, signal?: AbortSignal): Promise<ProcessingStatusDto> {
  const { data } = await http.get<ProcessingStatusDto>("/system/status", {
    params: { processId },
    signal,
  });
  return data;
}
