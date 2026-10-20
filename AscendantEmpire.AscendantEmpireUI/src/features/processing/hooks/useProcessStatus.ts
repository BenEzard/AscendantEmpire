import { useQuery } from "@tanstack/react-query";
import {
  getProcessStatus,
  TERMINAL_PROCESSING_STATUSES,
  type ProcessingStatusDto,
  type ProcessingStatusName,
} from "../api/processStatusApi";

/** Generic phase of any polled background process. */
export type ProcessPhase = "idle" | "polling" | "done" | "error";

const POLL_INTERVAL_MS = 2000;

/**
 * True once the server will not move the process on any further, whichever way it ended.
 *
 * Compared by name, because that is what the endpoint sends. A numeric comparison here does not fail loudly — it just
 * never matches, so a finished job reads as still running and both the polling and the terminal phase below are missed.
 */
function isTerminal(status: ProcessingStatusName | undefined): boolean {
  return status !== undefined && TERMINAL_PROCESSING_STATUSES.includes(status);
}

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
    refetchInterval: (q) => (isTerminal(q.state.data?.status) ? false : POLL_INTERVAL_MS),
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
    if (query.data.status === "Complete") {
      phase = "done";
    } else if (query.data.status === "Failed") {
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
