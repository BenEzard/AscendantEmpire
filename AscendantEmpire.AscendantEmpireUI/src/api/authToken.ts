// Single source of truth for the logged-in user's auth state in the browser.
//
// We keep one bearer token at a time in localStorage. After login it's a "user token"
// (identity only); after entering a game it's swapped for a "game token" that also carries
// the game and player. The http interceptor reads getToken() to attach it to requests.

const STORAGE_KEY = "ae_auth";

export type AuthState = {
  token: string;
  username: string;
  // Present only once a game has been entered (game token).
  gameId?: number | null;
  playerId?: number | null;
};

/** Reads the full auth state, or null when logged out. */
export function getAuth(): AuthState | null {
  const raw = localStorage.getItem(STORAGE_KEY);
  if (!raw) return null;

  try {
    return JSON.parse(raw) as AuthState;
  } catch {
    // Corrupt value — treat as logged out rather than crashing.
    return null;
  }
}

/** Persists the auth state, replacing whatever token was there before. */
export function setAuth(state: AuthState): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
}

/** Clears all auth state (logout, or after a 401). */
export function clearAuth(): void {
  localStorage.removeItem(STORAGE_KEY);
}

/** The bearer token to send, or null when logged out. */
export function getToken(): string | null {
  return getAuth()?.token ?? null;
}

/** True once a game token is held (the user has entered a game). */
export function hasGameContext(): boolean {
  const auth = getAuth();
  return auth?.gameId != null && auth?.playerId != null;
}
