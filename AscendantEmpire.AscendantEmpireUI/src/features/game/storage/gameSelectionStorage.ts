// Persists the UI's currently-open game selection across page refreshes.
//
// The game *token* (gameId + playerId) already survives a refresh via localStorage in
// `@/api/authToken`, so the backend knows which game we're in. But the UI also needs the
// display + layout fields that only live on the `OpenGameSelection` (game name, map width/height,
// ruler label, campaign offset) to re-render the board without forcing the user to re-pick the
// game. We store that selection here and, on load, only hand it back if it still matches the game
// token — that keeps the selection from outliving the token (e.g. after logout, or entering a
// different game) and becoming stale.

import { getAuth } from "@/api/authToken";
import type { OpenGameSelection } from "../types/OpenGameSelection";

const STORAGE_KEY = "ae_game_selection";

/** Persists the open-game selection, replacing any previously stored one. */
export function saveGameSelection(selection: OpenGameSelection): void {
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
  // token has moved on (logged out, or entered a different game), so the selection is stale.
  const auth = getAuth();
  if (auth?.gameId !== selection.gameId || auth?.playerId !== selection.playerId) {
    clearGameSelection();
    return null;
  }

  return selection;
}
