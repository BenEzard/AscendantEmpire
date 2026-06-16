// Single source of truth for the logged-in user's auth state in the browser.
//
// We keep one short-lived bearer (access) token at a time in localStorage, plus a longer-lived
// refresh token. After login the bearer is a "user token" (identity only); after entering a game
// it's swapped for a "game token" that also carries the game and player. The http interceptor
// reads getToken() to attach the bearer to requests, and uses the refresh token to transparently
// obtain a new bearer when the old one expires (a 401).
//
// Security note: storing the refresh token in localStorage exposes it to XSS, same as the access
// token already is here. A more secure design keeps it in an httpOnly cookie, at the cost of CORS
// credentials + CSRF handling. We keep localStorage for consistency with the current setup.

const STORAGE_KEY = "ae_auth";

export type AuthState = {
  token: string;
  // The refresh token; used to mint a new access token when `token` expires.
  refreshToken?: string | null;
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

/** The refresh token, or null when logged out / none held. */
export function getRefreshToken(): string | null {
  return getAuth()?.refreshToken ?? null;
}

/**
 * Replaces just the tokens after a refresh, keeping the rest of the auth state (username, game
 * context) intact. No-op when logged out.
 */
export function updateTokens(token: string, refreshToken: string): void {
  const current = getAuth();
  if (!current) return;

  setAuth({ ...current, token, refreshToken });
}

/** True once a game token is held (the user has entered a game). */
export function hasGameContext(): boolean {
  const auth = getAuth();
  return auth?.gameId != null && auth?.playerId != null;
}
