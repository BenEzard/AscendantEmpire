import { useQuery } from "@tanstack/react-query";
import { getProcessStatus, type ProcessingStatusDto } from "../api/processStatusApi";

/** Generic phase of any polled background process. */
export type ProcessPhase = "idle" | "polling" | "done" | "error";

// Mirrors the backend ProcessingStatus enum.
const PROCESSING_STATUS = {
  COMPLETE: 2,
  FAILED: 3,
} as const;

export interface UseProcessStatusResult {
  /** Derived phase of the process. */
  phase: ProcessPhase;
  /** Latest progress value (0–100). 0 while idle. */
  progressPercent: number;
  /** Populated when phase === "error". */
  errorMessage: string | null;
  /** Raw status DTO from the most recent poll, if any. */
  data: ProcessingStatusDto | undefined;
}

/**
 * Process-agnostic poller for GET /system/status. Pass a processId to start polling;
 * pass null to stay idle. Polls every 2s and stops automatically once the process
 * reaches a terminal (Complete/Failed) state.
 */
export function useProcessStatus(processId: number | null): UseProcessStatusResult {
  const query = useQuery<ProcessingStatusDto>({
    queryKey: ["system/status", processId],
    queryFn: ({ signal }) => getProcessStatus(processId!, signal),
    enabled: processId !== null,
    // Stop polling automatically once the job has reached a terminal state.
    refetchInterval: (q) => {
      const status = q.state.data?.status ?? 0;
      return status >= PROCESSING_STATUS.COMPLETE ? false : 2000;
    },
    refetchIntervalInBackground: true,
  });

  let phase: ProcessPhase = "idle";
  let errorMessage: string | null = null;

  if (processId === null) {
    phase = "idle";
  } else if (query.error) {
    phase = "error";
    errorMessage = query.error instanceof Error ? query.error.message : "Failed to check status.";
  } else if (query.data) {
    if (query.data.status === PROCESSING_STATUS.COMPLETE) {
      phase = "done";
    } else if (query.data.status === PROCESSING_STATUS.FAILED) {
      phase = "error";
      errorMessage = query.data.errorMessage ?? "The operation failed on the server.";
    } else {
      phase = "polling";
    }
  } else {
    // Enabled but no data yet — we're waiting on the first poll.
    phase = "polling";
  }

  return {
    phase,
    progressPercent: query.data?.progressPercent ?? 0,
    errorMessage,
    data: query.data,
  };
}
