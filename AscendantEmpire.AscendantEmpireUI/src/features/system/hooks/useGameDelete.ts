import { useCallback, useEffect, useState } from "react";
import { useMutation } from "@tanstack/react-query";

import { useProcessStatus } from "@/features/processing/hooks/useProcessStatus";

import { deleteGame } from "../api/gamesApi";

/**
 * Where a delete has got to.
 *
 * `starting` covers the DELETE call itself, `polling` the background purge it queued. The two are separate because
 * the endpoint returns as soon as the job is accepted — the game is still there at that point.
 */
export type GameDeletePhase =
  | { phase: "idle" }
  | { phase: "starting"; gameId: number }
  | { phase: "polling"; gameId: number; processId: number }
  | { phase: "done"; gameId: number }
  | { phase: "error"; gameId: number; message: string };

export interface UseGameDeleteResult {
  deletePhase: GameDeletePhase;
  /** The latest progress value (0–100) from the polling response. */
  progressPercent: number;
  /** The game currently being deleted, or null when idle or finished. */
  deletingGameId: number | null;
  /** Start deleting a game. Confirmation is the caller's job — this asks nothing. */
  startDelete: (gameId: number) => void;
  /** Reset back to idle (e.g. when the user closes the progress dialog). */
  resetDelete: () => void;
  /** True while any phase other than idle is active. */
  isActive: boolean;
}

const IDLE: GameDeletePhase = { phase: "idle" };

/**
 * Drives a game deletion: initiate → poll → done. Modelled on {@link useCatalogueExport}, minus the download step,
 * since a purge produces no file.
 */
export function useGameDelete(): UseGameDeleteResult {
  const [deletePhase, setDeletePhase] = useState<GameDeletePhase>(IDLE);
  // Held in local state so the last value survives the phase change to done, rather than snapping back to 0.
  const [progressPercent, setProgressPercent] = useState(0);

  // --- DELETE /games/{gameId} ---
  const initiateMutation = useMutation({
    mutationFn: (gameId: number) => deleteGame(gameId),
    onSuccess: (processId: number, gameId: number) => {
      setDeletePhase({ phase: "polling", gameId, processId });
    },
    onError: (err: unknown, gameId: number) => {
      const message = err instanceof Error ? err.message : "Failed to start the deletion.";
      setDeletePhase({ phase: "error", gameId, message });
    },
  });

  // --- Generic status polling (only while in the polling phase) ---
  const pollingProcessId = deletePhase.phase === "polling" ? deletePhase.processId : null;
  const poll = useProcessStatus(pollingProcessId);

  // Mirror polling progress into local state so it survives the terminal phases.
  useEffect(() => {
    if (deletePhase.phase === "polling") {
      setProgressPercent(poll.progressPercent);
    }
  }, [poll.progressPercent, deletePhase.phase]);

  // --- React to the generic poll reaching a terminal state ---
  useEffect(() => {
    if (deletePhase.phase !== "polling") {
      return;
    }

    if (poll.phase === "done") {
      setProgressPercent(100);
      setDeletePhase({ phase: "done", gameId: deletePhase.gameId });
    } else if (poll.phase === "error") {
      setDeletePhase({
        phase: "error",
        gameId: deletePhase.gameId,
        message: poll.errorMessage ?? "The deletion failed on the server.",
      });
    }
  }, [poll.phase, poll.errorMessage, deletePhase]);

  const startDelete = useCallback((gameId: number) => {
    setProgressPercent(0);
    setDeletePhase({ phase: "starting", gameId });
    initiateMutation.mutate(gameId);
  }, [initiateMutation]);

  const resetDelete = useCallback(() => {
    setDeletePhase(IDLE);
    setProgressPercent(0);
  }, []);

  // Only report a game as "being deleted" while the work is still going; once it is done or has failed the row
  // should go back to being an ordinary row (or disappear on the next reload).
  const deletingGameId = deletePhase.phase === "starting" || deletePhase.phase === "polling" ? deletePhase.gameId : null;

  return {
    deletePhase,
    progressPercent,
    deletingGameId,
    startDelete,
    resetDelete,
    isActive: deletePhase.phase !== "idle",
  };
}
