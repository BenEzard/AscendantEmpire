import { http } from "@/api/http";
import { setAuth } from "@/api/authToken";

// Mirrors the API's IAuthTokenResult (camelCased by ASP.NET's JSON serializer).
type AuthTokenResult = {
  token: string;
  username: string;
  gameId?: number | null;
  playerId?: number | null;
};

/**
 * Create a new account. On success the returned user token is stored and the user is
 * considered logged in (but hasn't entered a game yet).
 */
export async function register(username: string, password: string, email?: string): Promise<AuthTokenResult> {
  const { data } = await http.post<AuthTokenResult>("/auth/register", { username, password, email });
  setAuth({ token: data.token, username: data.username });
  return data;
}

/**
 * Log in with username + password. On success the user token is stored.
 */
export async function login(username: string, password: string): Promise<AuthTokenResult> {
  const { data } = await http.post<AuthTokenResult>("/auth/login", { username, password });
  setAuth({ token: data.token, username: data.username });
  return data;
}

/**
 * Enter a specific game. Requires being logged in already; swaps the stored user token for a
 * game token that carries the game id and the player the user controls in it.
 */
export async function enterGame(gameId: number): Promise<AuthTokenResult> {
  const { data } = await http.post<AuthTokenResult>("/auth/enter-game", { gameId });
  setAuth({ token: data.token, username: data.username, gameId: data.gameId, playerId: data.playerId });
  return data;
}
