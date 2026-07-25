import { useCallback, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { playerCampaignTilesKey } from "@/features/map/hooks/useCampaignMapTiles";
import { clearGameSelection, loadGameSelection, saveGameSelection } from "../storage/gameSelectionStorage";
import type { OpenGameSelection } from "../types/OpenGameSelection";

export type GameActiveDialog = "openGame" | "newGame" | null;

/**
 * Owns the currently selected game and the game-level dialog (open/new). The selection is
 * persisted to localStorage (see `gameSelectionStorage`) so a page refresh restores the open game
 * instead of forcing a re-pick; the initial state is rehydrated from there, but only when it still
 * matches the live game token. `closeGame` cancels + removes the open game's tile query so a late
 * response can't repopulate a closed game. Per-tool state (vision, force selection) self-resets on
 * game close via its own hooks watching `gameId`.
 */
export function useGameSession() {
  const queryClient = useQueryClient();
  // Lazy initializer: rehydrate the previously-open game on a refresh (validated against the token).
  const [gameSelection, setGameSelectionState] = useState<OpenGameSelection | null>(() => loadGameSelection());
  const [activeDialog, setActiveDialog] = useState<GameActiveDialog>(null);

  const gameId = gameSelection?.gameId;
  const playerId = gameSelection?.playerId;

  // Wraps the raw state setter so the persisted selection stays in sync: a selection is saved, a
  // clear removes it. This keeps every caller (open a game, close a game) from having to remember to
  // update localStorage separately.
  const setGameSelection = useCallback((selection: OpenGameSelection | null) => {
    if (selection == null) {
      clearGameSelection();
    } else {
      saveGameSelection(selection);
    }

    setGameSelectionState(selection);
  }, []);

  const closeGame = useCallback(() => {
    if (gameId != null && playerId != null) {
      const key = playerCampaignTilesKey(gameId, playerId);
      queryClient.cancelQueries({ queryKey: key });
      queryClient.removeQueries({ queryKey: key });
    }

    setGameSelection(null);
    setActiveDialog(null);
  }, [gameId, playerId, queryClient, setGameSelection]);

  return { gameSelection, setGameSelection, gameId, playerId, activeDialog, setActiveDialog, closeGame };
}
