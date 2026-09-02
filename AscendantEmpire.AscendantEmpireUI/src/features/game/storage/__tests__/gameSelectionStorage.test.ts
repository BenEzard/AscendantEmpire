import { clearAuth, setAuth } from "@/api/authToken";
import { clearGameSelection, loadGameSelection, matchesGameToken, saveGameSelection } from "../gameSelectionStorage";
import type { OpenGameSelection } from "../../types/OpenGameSelection";

const STORAGE_KEY = "ae_game_selection";

/** A selection for one player in game 1. The ids are what matter here; the rest is display detail. */
function selectionFor(playerId: number, gameId = 1): OpenGameSelection {
  return {
    gameId,
    gameName: "Test Game",
    width: 20,
    height: 20,
    playerId,
    rulerLabel: `Ruler ${playerId}`,
    campaignOffsetId: 3,
  };
}

/** Puts a game token in place — logged in, and in this game as this player. */
function holdGameToken(playerId: number, gameId = 1): void {
  setAuth({ token: "game-token", refreshToken: "refresh", username: "ben", gameId, playerId, permissions: [] });
}

/** Puts a user token in place — logged in, but no game entered, so the token names no game or player. */
function holdUserToken(): void {
  setAuth({ token: "user-token", refreshToken: "refresh", username: "ben", permissions: [] });
}

/**
 * The rule this file exists for: the persisted selection may never name a player the game token doesn't.
 *
 * The selection's playerId is what the game UI treats as "me" — it decides which forces are selectable and movable — so a
 * selection that outlives its token, or that was built from something other than the token, hands one user another user's
 * units. GM-10 was exactly that: log out, log in as someone else, and the previous player's identity was still in play.
 */
describe("gameSelectionStorage", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  describe("loadGameSelection", () => {
    it("restores the selection when it matches the game token", () => {
      holdGameToken(7);
      localStorage.setItem(STORAGE_KEY, JSON.stringify(selectionFor(7)));

      expect(loadGameSelection()?.playerId).toBe(7);
    });

    it("drops a selection naming a different player, so logging in as someone else can't inherit it", () => {
      // Jen (player 8) played and left her selection behind; Ben (player 7) is the one now holding a token.
      localStorage.setItem(STORAGE_KEY, JSON.stringify(selectionFor(8)));
      holdGameToken(7);

      expect(loadGameSelection()).toBeNull();
      expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
    });

    it("drops a selection naming a different game", () => {
      holdGameToken(7, 1);
      localStorage.setItem(STORAGE_KEY, JSON.stringify(selectionFor(7, 2)));

      expect(loadGameSelection()).toBeNull();
    });

    it("drops the selection when only a user token is held, because no game has been entered", () => {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(selectionFor(7)));
      holdUserToken();

      expect(loadGameSelection()).toBeNull();
      expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
    });

    it("drops the selection when logged out entirely", () => {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(selectionFor(7)));
      clearAuth();

      expect(loadGameSelection()).toBeNull();
    });

    it("returns null for a corrupt stored value rather than throwing", () => {
      holdGameToken(7);
      localStorage.setItem(STORAGE_KEY, "not json");

      expect(loadGameSelection()).toBeNull();
      expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
    });

    it("returns null when nothing has been stored", () => {
      holdGameToken(7);

      expect(loadGameSelection()).toBeNull();
    });
  });

  describe("saveGameSelection", () => {
    it("stores a selection that matches the game token", () => {
      holdGameToken(7);
      saveGameSelection(selectionFor(7));

      expect(loadGameSelection()?.playerId).toBe(7);
    });

    it("refuses to store a selection naming a player the token doesn't, so a bad one can't reach the next page load", () => {
      const consoleError = vi.spyOn(console, "error").mockImplementation(() => {});
      holdGameToken(7);

      saveGameSelection(selectionFor(8));

      expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
      expect(consoleError).toHaveBeenCalled();
      consoleError.mockRestore();
    });
  });

  describe("matchesGameToken", () => {
    it("matches only when both the game and the player agree with the token", () => {
      holdGameToken(7, 1);

      expect(matchesGameToken(selectionFor(7, 1))).toBe(true);
      expect(matchesGameToken(selectionFor(8, 1))).toBe(false);
      expect(matchesGameToken(selectionFor(7, 2))).toBe(false);
    });
  });

  describe("clearGameSelection", () => {
    it("removes the stored selection", () => {
      holdGameToken(7);
      saveGameSelection(selectionFor(7));

      clearGameSelection();

      expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
    });
  });
});
