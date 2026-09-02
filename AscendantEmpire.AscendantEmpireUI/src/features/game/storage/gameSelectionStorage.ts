// Persists the UI's currently-open game selection across page refreshes.
//
// The game *token* (gameId + playerId) already survives a refresh via localStorage in
// `@/api/authToken`, so the backend knows which game we're in. But the UI also needs the
// display + layout fields that only live on the `OpenGameSelection` (game name, map width/height,
// ruler label, campaign offset) to re-render the board without forcing the user to re-pick the
// game. We store that selection here and only ever hand it back — or take it in — while it still
// matches the game token, so a selection can never name a player the token doesn't.
//
// That last rule is load-bearing rather than tidiness. The selection's `playerId` is what the whole
// game UI treats as "me": it decides which forces are selectable, which can be ordered to move, and
// which the toolbars offer actions for. A selection naming a different player than the token does
// hands one user another user's units to play with, so the two are kept in lockstep here.

import { getAuth } from "@/api/authToken";
import type { OpenGameSelection } from "../types/OpenGameSelection";

const STORAGE_KEY = "ae_game_selection";

/**
 * True when the selection names exactly the game and player the current game token does.
 *
 * A user token (logged in, but no game entered) carries neither, so nothing matches it — which is
 * what stops a selection surviving a logout, or a login as somebody else.
 */
export function matchesGameToken(selection: OpenGameSelection): boolean {
  const auth = getAuth();

  return auth?.gameId === selection.gameId && auth?.playerId === selection.playerId;
}

/**
 * Persists the open-game selection, replacing any previously stored one.
 *
 * A selection that doesn't match the live game token is dropped rather than stored: whoever built it
 * took the player from somewhere other than the token, and persisting it would let that wrong
 * identity outlive the mistake by surviving into the next page load.
 */
export function saveGameSelection(selection: OpenGameSelection): void {
  if (!matchesGameToken(selection)) {
    console.error("Refusing to store a game selection that does not match the game token.", selection);
    return;
  }

  localStorage.setItem(STORAGE_KEY, JSON.stringify(selection));
}

/** Clears the persisted selection (game closed, or found to be stale on load). */
export function clearGameSelection(): void {
  localStorage.removeItem(STORAGE_KEY);
}

/**
 * Reads the persisted selection, or null when there's nothing usable. Returns null (and clears the
 * stale value) unless the stored selection matches the current game token's gameId + playerId, so a
 * selection can never outlive the token it belongs to.
 */
export function loadGameSelection(): OpenGameSelection | null {
  const raw = localStorage.getItem(STORAGE_KEY);
  if (!raw) {
    return null;
  }

  let selection: OpenGameSelection;
  try {
    selection = JSON.parse(raw) as OpenGameSelection;
  } catch {
    // Corrupt value — drop it rather than crashing on load.
    clearGameSelection();
    return null;
  }

  // Only restore if the stored selection still matches the live game token. A mismatch means the
  // token has moved on (logged out, logged in as someone else, or entered a different game), so the
  // selection is stale.
  if (!matchesGameToken(selection)) {
    clearGameSelection();
    return null;
  }

  return selection;
}
