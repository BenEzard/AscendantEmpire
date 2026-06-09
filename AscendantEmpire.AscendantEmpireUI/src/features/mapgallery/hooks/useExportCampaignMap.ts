import { useCallback, useEffect, useRef, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import {
  getMapExportDownload,
  getProcessingStatus,
  postInitiateMapExport,
  type ProcessingStatusDto,
} from "../api/exportCampaignMapApi";

export type ExportPhase =
  | { phase: "idle" }
  | { phase: "starting" }
  | { phase: "polling"; processId: number }
  | { phase: "downloading"; processId: number }
  | { phase: "done" }
  | { phase: "error"; message: string };

export interface UseExportCampaignMapResult {
  exportPhase: ExportPhase;
  /** The latest progress value (0–100) from the polling response. */
  progressPercent: number;
  /** Initiate an export for the given map gallery entry. */
  startExport: (mapGalleryId: number) => void;
  /** Reset back to idle (e.g. when the user closes the dialog). */
  resetExport: () => void;
  /** True while any export phase other than idle is active. */
  isActive: boolean;
}

const IDLE: ExportPhase = { phase: "idle" };
const PROCESSING_STATUS = {
  COMPLETE: 2,
  FAILED: 3,
} as const;

export function useExportCampaignMap(): UseExportCampaignMapResult {
  const [exportPhase, setExportPhase] = useState<ExportPhase>(IDLE);
  const [progressPercent, setProgressPercent] = useState(0);

  // Keep the map gallery ID stable across async callbacks without causing re-renders.
  const mapGalleryIdRef = useRef<number | null>(null);

  // --- POST /map-gallery/{id}/export ---
  const initiateMutation = useMutation({
    mutationFn: (id: number) => postInitiateMapExport(id),
    onSuccess: (processId: number) => {
      setExportPhase({ phase: "polling", processId });
    },
    onError: (err: unknown) => {
      const message = err instanceof Error ? err.message : "Failed to start export.";
      setExportPhase({ phase: "error", message });
    },
  });

  // --- GET /system/status?processId={n} polling ---
  const pollingProcessId = exportPhase.phase === "polling" ? exportPhase.processId : null;

  const statusQuery = useQuery<ProcessingStatusDto>({
    queryKey: ["system/status", pollingProcessId],
    queryFn: ({ signal }) => getProcessingStatus(pollingProcessId!, signal),
    enabled: pollingProcessId !== null,
    // Stop polling automatically once the job has reached a terminal state.
    refetchInterval: (query) => {
      const status = query.state.data?.status ?? 0;
      return status >= PROCESSING_STATUS.COMPLETE ? false : 2000;
    },
    refetchIntervalInBackground: true,
  });

  // --- Trigger file download ---
  const triggerDownload = useCallback(async (mapId: number, processId: number) => {
    try {
      const { blob, fileName } = await getMapExportDownload(mapId, processId);
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = fileName;
      document.body.appendChild(anchor);
      anchor.click();
      document.body.removeChild(anchor);
      URL.revokeObjectURL(url);
      setExportPhase({ phase: "done" });
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : "Failed to download export.";
      setExportPhase({ phase: "error", message });
    }
  }, []);

  // --- React to polling data (TanStack Query v5 — no onSuccess on useQuery) ---
  useEffect(() => {
    const data = statusQuery.data;
    if (!data || exportPhase.phase !== "polling") {
      return;
    }

    setProgressPercent(data.progressPercent);

    if (data.status === PROCESSING_STATUS.COMPLETE) {
      const mapId = mapGalleryIdRef.current;
      if (mapId === null) {
        return;
      }
      setExportPhase({ phase: "downloading", processId: data.processingId });
      void triggerDownload(mapId, data.processingId);
    } else if (data.status === PROCESSING_STATUS.FAILED) {
      setExportPhase({
        phase: "error",
        message: data.errorMessage ?? "Export failed on the server.",
      });
    }
  }, [statusQuery.data]);

  // React to polling errors.
  useEffect(() => {
    if (!statusQuery.error || exportPhase.phase !== "polling") {
      return;
    }
    const message =
      statusQuery.error instanceof Error ? statusQuery.error.message : "Failed to check export status.";
    setExportPhase({ phase: "error", message });
  }, [statusQuery.error]);

  const startExport = useCallback((mapGalleryId: number) => {
    mapGalleryIdRef.current = mapGalleryId;
    setProgressPercent(0);
    setExportPhase({ phase: "starting" });
    initiateMutation.mutate(mapGalleryId);
  }, [initiateMutation]);

  const resetExport = useCallback(() => {
    setExportPhase(IDLE);
    setProgressPercent(0);
    mapGalleryIdRef.current = null;
  }, []);

  const isActive = exportPhase.phase !== "idle";

  return { exportPhase, progressPercent, startExport, resetExport, isActive };
}
