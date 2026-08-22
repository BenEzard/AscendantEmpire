import { useCallback, useEffect, useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { postInitiateCatalogueImport } from "../api/catalogueTransferApi";
import { useProcessStatus } from "@/features/processing/hooks/useProcessStatus";

export type CatalogueImportPhase =
  | { phase: "idle" }
  | { phase: "starting" }
  | { phase: "polling"; processId: number }
  | { phase: "done" }
  | { phase: "error"; message: string };

export interface UseCatalogueImportResult {
  importPhase: CatalogueImportPhase;
  /** The latest progress value (0–100) from the polling response. */
  progressPercent: number;
  /** Initiate an import from the given exported zip file. */
  startImport: (file: File) => void;
  /** Reset back to idle (e.g. when the user closes the dialog). */
  resetImport: () => void;
  /** True while any import phase other than idle is active. */
  isActive: boolean;
}

const IDLE: CatalogueImportPhase = { phase: "idle" };

/** Drives a catalogue import: initiate (upload) → poll → done. Mirrors the map-gallery import hook. */
export function useCatalogueImport(): UseCatalogueImportResult {
  const [importPhase, setImportPhase] = useState<CatalogueImportPhase>(IDLE);
  // Held in local state so it survives once polling stops at the terminal phase.
  const [progressPercent, setProgressPercent] = useState(0);

  // --- POST /unit-design-transfer/import ---
  const initiateMutation = useMutation({
    mutationFn: (file: File) => postInitiateCatalogueImport(file),
    onSuccess: (processId: number) => {
      setImportPhase({ phase: "polling", processId });
    },
    onError: (err: unknown) => {
      const message = err instanceof Error ? err.message : "Failed to start import.";
      setImportPhase({ phase: "error", message });
    },
  });

  // --- Generic status polling (only while in the polling phase) ---
  const pollingProcessId = importPhase.phase === "polling" ? importPhase.processId : null;
  const poll = useProcessStatus(pollingProcessId);

  // Mirror polling progress into local state.
  useEffect(() => {
    if (importPhase.phase === "polling") {
      setProgressPercent(poll.progressPercent);
    }
  }, [poll.progressPercent, importPhase.phase]);

  // --- React to the generic poll reaching a terminal state ---
  useEffect(() => {
    if (importPhase.phase !== "polling") {
      return;
    }

    if (poll.phase === "done") {
      setProgressPercent(100);
      setImportPhase({ phase: "done" });
    } else if (poll.phase === "error") {
      setImportPhase({
        phase: "error",
        message: poll.errorMessage ?? "Import failed on the server.",
      });
    }
  }, [poll.phase, poll.errorMessage, importPhase.phase]);

  const startImport = useCallback((file: File) => {
    setProgressPercent(0);
    setImportPhase({ phase: "starting" });
    initiateMutation.mutate(file);
  }, [initiateMutation]);

  const resetImport = useCallback(() => {
    setImportPhase(IDLE);
    setProgressPercent(0);
  }, []);

  const isActive = importPhase.phase !== "idle";

  return { importPhase, progressPercent, startImport, resetImport, isActive };
}
