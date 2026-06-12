import { http } from "@/api/http";

/** Mirrors the backend DTProcessingStatus returned by GET /system/status. */
export interface ProcessingStatusDto {
  processingId: number;
  processType: number;
  /** 0=Pending, 1=InProgress, 2=Complete, 3=Failed */
  status: number;
  /** 0–100, updated by the background processing service at key steps. */
  progressPercent: number;
  startedAt: string;
  completedAt: string | null;
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
