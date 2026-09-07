import { http } from "../../../api/http";
import type { MyGamesResponse } from "../types/MyGamesResponse";

/**
 * Fetches the games the logged-in account holds an active player slot in.
 *
 * There is nothing to send: the server reads the account from the bearer token, which is the only
 * trustworthy source for it. A game id or player id in a request here would just be a filter the
 * caller could point at somebody else's games.
 */
export async function getMyGames(): Promise<MyGamesResponse> {
  const res = await http.get<MyGamesResponse>("/games/mine");

  return res.data;
}
