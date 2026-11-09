import { useCallback, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { playerCampaignTilesKey } from "@/features/map/hooks/useCampaignMapTiles";
import { clearGameSelection, loadGameSelection, matchesGameToken, saveGameSelection } from "../storage/gameSelectionStorage";
import type { OpenGameSelection } from "../types/OpenGameSelection";

export type GameActiveDialog = "openGame" | null;

/**
 * Owns the currently selected game and the game-level dialog. The selection is
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
  //
  // A selection naming a player the game token doesn't is refused outright rather than half-applied.
  // The selection's playerId is what the game UI treats as "me", so letting a mismatched one into
  // state would offer this user another player's forces — the storage layer refuses to persist it
  // for the same reason, and the two must agree or a refresh would silently change who you are.
  const setGameSelection = useCallback((selection: OpenGameSelection | null) => {
    if (selection == null) {
      clearGameSelection();
      setGameSelectionState(null);
      return;
    }

    if (!matchesGameToken(selection)) {
      console.error("Refusing to open a game selection that does not match the game token.", selection);
      return;
    }

    saveGameSelection(selection);
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
