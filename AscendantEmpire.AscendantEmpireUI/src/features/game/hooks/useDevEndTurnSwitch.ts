// Remove in F-18. Drives the two temporary Dev Tools buttons that end a turn and continue as the next player. The whole file goes when F-18 is done.

import { useEffect } from "react";
import { useMutation } from "@tanstack/react-query";
import { setAuth } from "@/api/authToken";
import type { ToastVariant } from "@/hooks/useToast";
import { postEndTurnAndSwitchPlayer, type DevSwitchPlayerResult } from "../api/devTurnApi";
import { saveGameSelection } from "../storage/gameSelectionStorage";

/**
 * Carries the "you are now playing as ..." message across the page reload that completes the switch.
 *
 * Remove in F-18. sessionStorage rather than state because the reload throws every bit of state away, and a switch that said nothing at all would leave
 * the developer guessing which seat they had landed in.
 */
const PENDING_MESSAGE_KEY = "ae_dev_switch_message";

export interface UseDevEndTurnSwitchArgs {
  /** Shown once the page has come back up as the new player, or immediately when the switch fails. */
  openToast: (message: string, variant?: ToastVariant) => void;
}

export interface UseDevEndTurnSwitch {
  /** End this player's turn and continue as whoever plays next. */
  endTurnAndSwitchPlayer: () => void;
  /** End every turn until it is this player's turn again, and continue as them. */
  endRoundAndReturnToPlayer: () => void;
  /** True while a switch is in flight, so the buttons can be disabled rather than pressed twice. */
  isSwitching: boolean;
}

/**
 * Ends the current player's turn and brings the whole client back up as the player who goes next.
 *
 * Remove in F-18. The switch is finished with a full page reload rather than by patching state in place, for the same reason logging out is: every cached
 * query, in-flight request and piece of per-tool state belongs to the player who has just handed over, and reloading is the one way to be certain none of
 * it survives into the next player's board.
 *
 * The new game selection is written to storage before the reload, and rehydrated by `useGameSession` on the way back up — which only accepts it because
 * the token stored alongside it now names the same game and player.
 */
export function useDevEndTurnSwitch({ openToast }: UseDevEndTurnSwitchArgs): UseDevEndTurnSwitch {
  // Drain whatever the switch before this reload left to say. Runs once on mount, and does nothing at all on an ordinary page load.
  useEffect(() => {
    const message = sessionStorage.getItem(PENDING_MESSAGE_KEY);
    if (!message) {
      return;
    }

    sessionStorage.removeItem(PENDING_MESSAGE_KEY);
    openToast(message, "info");
  }, [openToast]);

  const switchPlayer = useMutation<DevSwitchPlayerResult, unknown, boolean>({
    mutationFn: (cycleWholeRound) => postEndTurnAndSwitchPlayer(cycleWholeRound),
    onSuccess: (result) => {
      // Take the new player's session first. Everything below is only allowed to be stored because this token now says who we are.
      setAuth({
        token: result.token,
        refreshToken: result.refreshToken,
        username: result.username,
        gameId: result.gameId,
        playerId: result.playerId,
        permissions: result.permissions,
      });

      // The same selection the Open Game dialog would have built, so the board redraws for the new player without them having to pick the game again.
      saveGameSelection({
        gameId: result.gameId,
        gameName: result.gameName,
        width: result.mapWidth,
        height: result.mapHeight,
        playerId: result.playerId,
        rulerLabel: result.rulerLabel,
        campaignOffsetId: result.campaignOffsetId,
      });

      const turns = result.turnsEnded === 1 ? "1 turn ended" : `${result.turnsEnded} turns ended`;
      sessionStorage.setItem(PENDING_MESSAGE_KEY, `${turns}. Now playing as ${result.rulerLabel} (${result.username}).`);

      window.location.reload();
    },
    // The API writes its refusals to be read as they stand, so they go straight to the developer rather than being reworded here.
    onError: (error) => openToast(error instanceof Error ? error.message : "The turn could not be ended.", "danger"),
  });

  return {
    endTurnAndSwitchPlayer: () => switchPlayer.mutate(false),
    endRoundAndReturnToPlayer: () => switchPlayer.mutate(true),
    isSwitching: switchPlayer.isPending,
  };
}
