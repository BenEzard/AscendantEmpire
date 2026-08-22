import { useCallback, useEffect, useState } from "react";
import { useMutation } from "@tanstack/react-query";
import {
  getCatalogueExportDownload,
  postInitiateCatalogueExport,
  type CatalogueExportSelection,
} from "../api/catalogueTransferApi";
import { useProcessStatus } from "@/features/processing/hooks/useProcessStatus";

export type CatalogueExportPhase =
  | { phase: "idle" }
  | { phase: "starting" }
  | { phase: "polling"; processId: number }
  | { phase: "downloading"; processId: number }
  | { phase: "done" }
  | { phase: "error"; message: string };

export interface UseCatalogueExportResult {
  exportPhase: CatalogueExportPhase;
  /** The latest progress value (0–100) from the polling response. */
  progressPercent: number;
  /** Initiate an export for the given catalogue selection. */
  startExport: (selection: CatalogueExportSelection) => void;
  /** Reset back to idle (e.g. when the user closes the dialog). */
  resetExport: () => void;
  /** True while any export phase other than idle is active. */
  isActive: boolean;
}

const IDLE: CatalogueExportPhase = { phase: "idle" };

/**
 * Drives a catalogue export: initiate → poll → auto-download. Mirrors the map-gallery export hook, but the
 * download only needs the processId (the zip filename identifies itself), so no per-entity id is tracked.
 */
export function useCatalogueExport(): UseCatalogueExportResult {
  const [exportPhase, setExportPhase] = useState<CatalogueExportPhase>(IDLE);
  // Held in local state so it persists through the download phase (when polling has stopped).
  const [progressPercent, setProgressPercent] = useState(0);

  // --- POST /unit-design-transfer/export ---
  const initiateMutation = useMutation({
    mutationFn: (selection: CatalogueExportSelection) => postInitiateCatalogueExport(selection),
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
  const triggerDownload = useCallback(async (processId: number) => {
    try {
      const { blob, fileName } = await getCatalogueExportDownload(processId);
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
        // Always release the object URL, even if appending/clicking the anchor throws.
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
      setExportPhase({ phase: "downloading", processId: exportPhase.processId });
      void triggerDownload(exportPhase.processId);
    } else if (poll.phase === "error") {
      setExportPhase({
        phase: "error",
        message: poll.errorMessage ?? "Export failed on the server.",
      });
    }
  }, [poll.phase, poll.errorMessage, exportPhase, triggerDownload]);

  const startExport = useCallback((selection: CatalogueExportSelection) => {
    setProgressPercent(0);
    setExportPhase({ phase: "starting" });
    initiateMutation.mutate(selection);
  }, [initiateMutation]);

  const resetExport = useCallback(() => {
    setExportPhase(IDLE);
    setProgressPercent(0);
  }, []);

  const isActive = exportPhase.phase !== "idle";

  return { exportPhase, progressPercent, startExport, resetExport, isActive };
}
