import { http } from "@/api/http";
import { getAuth, setAuth } from "@/api/authToken";

// Mirrors the API's IAuthTokenResult (camelCased by ASP.NET's JSON serializer).
type AuthTokenResult = {
  token: string;
  refreshToken?: string | null;
  username: string;
  gameId?: number | null;
  playerId?: number | null;
};

/**
 * Create a new account. On success the returned access + refresh tokens are stored and the user
 * is considered logged in (but hasn't entered a game yet).
 */
export async function register(username: string, password: string, email?: string): Promise<AuthTokenResult> {
  const { data } = await http.post<AuthTokenResult>("/auth/register", { username, password, email });
  setAuth({ token: data.token, refreshToken: data.refreshToken, username: data.username });
  return data;
}

/**
 * Log in with username + password. On success the access + refresh tokens are stored.
 */
export async function login(username: string, password: string): Promise<AuthTokenResult> {
  const { data } = await http.post<AuthTokenResult>("/auth/login", { username, password });
  setAuth({ token: data.token, refreshToken: data.refreshToken, username: data.username });
  return data;
}

/**
 * Enter a specific game. Requires being logged in already; swaps the stored user token for a
 * game token that carries the game id and the player the user controls in it. The refresh token
 * is unchanged (same session), so we keep the one we already hold.
 */
export async function enterGame(gameId: number): Promise<AuthTokenResult> {
  const { data } = await http.post<AuthTokenResult>("/auth/enter-game", { gameId });
  const existingRefreshToken = getAuth()?.refreshToken ?? null;
  setAuth({
    token: data.token,
    refreshToken: existingRefreshToken,
    username: data.username,
    gameId: data.gameId,
    playerId: data.playerId,
  });
  return data;
}

/**
 * Revoke the current session server-side, then clear local auth. Best-effort: even if the call
 * fails (e.g. the access token already expired) the caller still clears local state.
 */
export async function logout(): Promise<void> {
  await http.post("/auth/logout");
}
