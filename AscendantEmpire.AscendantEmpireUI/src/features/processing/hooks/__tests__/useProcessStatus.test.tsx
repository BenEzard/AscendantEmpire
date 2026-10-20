import type { ReactNode } from "react";
import { act, renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import { getProcessStatus, type ProcessingStatusDto, type ProcessingStatusName } from "../../api/processStatusApi";
import { useProcessStatus } from "../useProcessStatus";

// Only the call is replaced. TERMINAL_PROCESSING_STATUSES stays real: it is half of what these tests are about, and a
// stub of it would pass against whatever the stub happened to claim.
vi.mock("../../api/processStatusApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../../api/processStatusApi")>()),
  getProcessStatus: vi.fn(),
}));

const PROCESS_ID = 43;
const POLL_INTERVAL_MS = 2000;
const FAILURE =
  'The DELETE statement conflicted with the REFERENCE constraint "FK_ResearchHistory_PlayerTurn_PlayerTurnId".';

const getProcessStatusMock = vi.mocked(getProcessStatus);

function wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}

/**
 * A status payload spelled the way the API actually sends one — enums by their member name.
 *
 * That is the whole point of this fixture. A payload built with numeric statuses would be a mock of a contract the
 * endpoint does not serve, and every assertion below would be testing the mock rather than the hook.
 */
function aStatus(status: ProcessingStatusName, overrides: Partial<ProcessingStatusDto> = {}): ProcessingStatusDto {
  return {
    processingId: PROCESS_ID,
    processType: "DeleteGame",
    status,
    progressPercent: 45,
    startedAt: "2026-10-21T08:40:00Z",
    completedAt: null,
    errorMessage: null,
    ...overrides,
  };
}

beforeEach(() => {
  getProcessStatusMock.mockReset();
});

describe("useProcessStatus", () => {
  it("stays idle and asks the server nothing without a process id", () => {
    const { result } = renderHook(() => useProcessStatus(null), { wrapper });

    expect(result.current.phase).toBe("idle");
    expect(result.current.progressPercent).toBe(0);
    expect(getProcessStatusMock).not.toHaveBeenCalled();
  });

  it("reports a job still running as polling, with its progress", async () => {
    getProcessStatusMock.mockResolvedValue(aStatus("InProgress", { progressPercent: 30 }));

    const { result } = renderHook(() => useProcessStatus(PROCESS_ID), { wrapper });

    await waitFor(() => expect(result.current.progressPercent).toBe(30));
    expect(result.current.phase).toBe("polling");
    expect(result.current.errorMessage).toBeNull();
  });

  it("reports a finished job as done", async () => {
    getProcessStatusMock.mockResolvedValue(aStatus("Complete", { progressPercent: 100 }));

    const { result } = renderHook(() => useProcessStatus(PROCESS_ID), { wrapper });

    await waitFor(() => expect(result.current.phase).toBe("done"));
    expect(result.current.errorMessage).toBeNull();
  });

  it("reports a failed job as an error and surfaces the message the server recorded", async () => {
    getProcessStatusMock.mockResolvedValue(aStatus("Failed", { errorMessage: FAILURE }));

    const { result } = renderHook(() => useProcessStatus(PROCESS_ID), { wrapper });

    await waitFor(() => expect(result.current.phase).toBe("error"));
    expect(result.current.errorMessage).toBe(FAILURE);
  });

  it("falls back to a message of its own when a failure carries none", async () => {
    getProcessStatusMock.mockResolvedValue(aStatus("Failed"));

    const { result } = renderHook(() => useProcessStatus(PROCESS_ID), { wrapper });

    await waitFor(() => expect(result.current.phase).toBe("error"));
    expect(result.current.errorMessage).toBe("The operation failed on the server.");
  });

  it("reports a status poll that itself failed as an error", async () => {
    getProcessStatusMock.mockRejectedValue(new Error("Network Error"));

    const { result } = renderHook(() => useProcessStatus(PROCESS_ID), { wrapper });

    await waitFor(() => expect(result.current.phase).toBe("error"));
    expect(result.current.errorMessage).toBe("Network Error");
  });
});

/**
 * The interval guard, which reads the same status and so broke the same way: a finished job that still reads as running
 * leaves the hook polling a terminal record every two seconds for as long as the dialog is open.
 */
describe("useProcessStatus polling", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  /** Advances past one poll interval inside act, so the refetch it triggers is flushed before anything is asserted. */
  async function runOutOnePollInterval() {
    await act(async () => {
      await vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS);
    });
  }

  it("keeps polling while the job is still running", async () => {
    getProcessStatusMock.mockResolvedValue(aStatus("InProgress"));

    renderHook(() => useProcessStatus(PROCESS_ID), { wrapper });

    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    expect(getProcessStatusMock).toHaveBeenCalledTimes(1);

    await runOutOnePollInterval();
    expect(getProcessStatusMock).toHaveBeenCalledTimes(2);
  });

  it("stops polling once the job has finished", async () => {
    getProcessStatusMock.mockResolvedValue(aStatus("Complete", { progressPercent: 100 }));

    renderHook(() => useProcessStatus(PROCESS_ID), { wrapper });

    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    expect(getProcessStatusMock).toHaveBeenCalledTimes(1);

    await runOutOnePollInterval();
    await runOutOnePollInterval();
    expect(getProcessStatusMock).toHaveBeenCalledTimes(1);
  });

  it("stops polling once the job has failed", async () => {
    getProcessStatusMock.mockResolvedValue(aStatus("Failed", { errorMessage: FAILURE }));

    renderHook(() => useProcessStatus(PROCESS_ID), { wrapper });

    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    expect(getProcessStatusMock).toHaveBeenCalledTimes(1);

    await runOutOnePollInterval();
    expect(getProcessStatusMock).toHaveBeenCalledTimes(1);
  });
});
