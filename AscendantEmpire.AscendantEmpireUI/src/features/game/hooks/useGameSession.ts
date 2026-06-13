import { useCallback, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { playerCampaignTilesKey } from "@/features/map/hooks/useCampaignMapTiles";
import type { OpenGameSelection } from "../types/OpenGameSelection";

export type GameActiveDialog = "openGame" | "newGame" | null;

/**
 * Owns the currently selected game and the game-level dialog (open/new). `closeGame`
 * cancels + removes the open game's tile query so a late response can't repopulate a
 * closed game. Per-tool state (vision, force selection) self-resets on game close via
 * its own hooks watching `gameId`.
 */
export function useGameSession() {
  const queryClient = useQueryClient();
  const [gameSelection, setGameSelection] = useState<OpenGameSelection | null>(null);
  const [activeDialog, setActiveDialog] = useState<GameActiveDialog>(null);

  const gameId = gameSelection?.gameId;
  const playerId = gameSelection?.playerId;

  const closeGame = useCallback(() => {
    if (gameId != null && playerId != null) {
      const key = playerCampaignTilesKey(gameId, playerId);
      queryClient.cancelQueries({ queryKey: key });
      queryClient.removeQueries({ queryKey: key });
    }

    setGameSelection(null);
    setActiveDialog(null);
  }, [gameId, playerId, queryClient]);

  return { gameSelection, setGameSelection, gameId, playerId, activeDialog, setActiveDialog, closeGame };
}
