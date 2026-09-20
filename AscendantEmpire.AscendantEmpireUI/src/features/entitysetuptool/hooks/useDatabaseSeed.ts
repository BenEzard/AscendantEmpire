import { useCallback, useEffect, useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { postInitiateDatabaseSeed, type SeedTarget } from "../api/seedApi";
import { useProcessStatus } from "@/features/processing/hooks/useProcessStatus";

export type DatabaseSeedPhase =
  | { phase: "idle" }
  | { phase: "starting" }
  | { phase: "polling"; processId: number }
  | { phase: "done" }
  | { phase: "error"; message: string };

export interface UseDatabaseSeedResult {
  seedPhase: DatabaseSeedPhase;
  /** The latest progress value (0–100) from the polling response. */
  progressPercent: number;
  /** Initiate a database seed over the given targets. Prerequisites are added server-side. */
  startSeed: (targets: SeedTarget[]) => void;
  /** Reset back to idle (e.g. when the user closes the dialog). */
  resetSeed: () => void;
  /** True while any seed phase other than idle is active. */
  isActive: boolean;
}

const IDLE: DatabaseSeedPhase = { phase: "idle" };

/** Drives a database seed: initiate → poll → done. Mirrors the catalogue-import hook, minus the upload. */
export function useDatabaseSeed(): UseDatabaseSeedResult {
  const [seedPhase, setSeedPhase] = useState<DatabaseSeedPhase>(IDLE);
  // Held in local state so it survives once polling stops at the terminal phase.
  const [progressPercent, setProgressPercent] = useState(0);

  // --- POST /system/seed ---
  const initiateMutation = useMutation({
    mutationFn: (targets: SeedTarget[]) => postInitiateDatabaseSeed(targets),
    onSuccess: (processId: number) => {
      setSeedPhase({ phase: "polling", processId });
    },
    onError: (err: unknown) => {
      const message = err instanceof Error ? err.message : "Failed to start seed.";
      setSeedPhase({ phase: "error", message });
    },
  });

  // --- Generic status polling (only while in the polling phase) ---
  const pollingProcessId = seedPhase.phase === "polling" ? seedPhase.processId : null;
  const poll = useProcessStatus(pollingProcessId);

  // Mirror polling progress into local state.
  useEffect(() => {
    if (seedPhase.phase === "polling") {
      setProgressPercent(poll.progressPercent);
    }
  }, [poll.progressPercent, seedPhase.phase]);

  // --- React to the generic poll reaching a terminal state ---
  useEffect(() => {
    if (seedPhase.phase !== "polling") {
      return;
    }

    if (poll.phase === "done") {
      setProgressPercent(100);
      setSeedPhase({ phase: "done" });
    } else if (poll.phase === "error") {
      setSeedPhase({
        phase: "error",
        message: poll.errorMessage ?? "Seed failed on the server.",
      });
    }
  }, [poll.phase, poll.errorMessage, seedPhase.phase]);

  const startSeed = useCallback(
    (targets: SeedTarget[]) => {
      setProgressPercent(0);
      setSeedPhase({ phase: "starting" });
      initiateMutation.mutate(targets);
    },
    [initiateMutation],
  );

  const resetSeed = useCallback(() => {
    setSeedPhase(IDLE);
    setProgressPercent(0);
  }, []);

  const isActive = seedPhase.phase !== "idle";

  return { seedPhase, progressPercent, startSeed, resetSeed, isActive };
}
