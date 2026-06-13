import { useCallback, useEffect, useRef, useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { getMapExportDownload, postInitiateMapExport } from "../api/exportCampaignMapApi";
import { useProcessStatus } from "@/features/processing/hooks/useProcessStatus";

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

export function useExportCampaignMap(): UseExportCampaignMapResult {
  const [exportPhase, setExportPhase] = useState<ExportPhase>(IDLE);
  // Held in local state so it persists through the download phase (when polling has stopped).
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

  // --- Generic status polling (only while in the polling phase) ---
  const pollingProcessId = exportPhase.phase === "polling" ? exportPhase.processId : null;
  const poll = useProcessStatus(pollingProcessId);

  // Mirror polling progress into local state so it survives the download phase.
  useEffect(() => {
    if (exportPhase.phase === "polling") {
      setProgressPercent(poll.progressPercent);
    }
  }, [poll.progressPercent, exportPhase.phase]);

  // --- Trigger file download ---
  const triggerDownload = useCallback(async (mapId: number, processId: number) => {
    try {
      const { blob, fileName } = await getMapExportDownload(mapId, processId);
      const url = URL.createObjectURL(blob);
      try {
        const anchor = document.createElement("a");
        anchor.href = url;
        anchor.download = fileName;
        document.body.appendChild(anchor);
        anchor.click();
        document.body.removeChild(anchor);
        setExportPhase({ phase: "done" });
      } finally {
        // Always release the object URL, even if appending/clicking the anchor throws,
        // so the blob isn't retained in memory.
        URL.revokeObjectURL(url);
      }
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : "Failed to download export.";
      setExportPhase({ phase: "error", message });
    }
  }, []);

  // --- React to the generic poll reaching a terminal state ---
  useEffect(() => {
    if (exportPhase.phase !== "polling") {
      return;
    }

    if (poll.phase === "done") {
      const mapId = mapGalleryIdRef.current;
      if (mapId === null) {
        return;
      }
      setExportPhase({ phase: "downloading", processId: exportPhase.processId });
      void triggerDownload(mapId, exportPhase.processId);
    } else if (poll.phase === "error") {
      setExportPhase({
        phase: "error",
        message: poll.errorMessage ?? "Export failed on the server.",
      });
    }
  }, [poll.phase, poll.errorMessage, exportPhase, triggerDownload]);

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
