import { http } from "@/api/http";
import type { MatchmakingGamesResponse } from "../types/MatchmakingGamesResponse";

/**
 * Fetch the games that can be joined, with each active slot flagged as claimed or free.
 * Requires a user token (the http interceptor attaches it); the account is read server-side.
 */
export async function getMatchmakingGames(): Promise<MatchmakingGamesResponse> {
  const { data } = await http.get<MatchmakingGamesResponse>("/matchmaking/games");
  return data;
}

/**
 * Claim an unassigned player slot for the logged-in user. Resolves on success (HTTP 204) and
 * rejects with a readable message on failure (e.g. the slot was taken, or the user already has
 * a slot in that game).
 */
export async function claimPlayerSlot(gameId: number, playerId: number): Promise<void> {
  await http.post("/matchmaking/claim", { gameId, playerId });
}
