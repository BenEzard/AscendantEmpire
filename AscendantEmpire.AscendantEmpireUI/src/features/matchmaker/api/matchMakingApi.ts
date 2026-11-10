import { http } from "@/api/http";
import type { MatchMakingGamesResponse } from "../types/MatchMakingGamesResponse";
import type { PlayerDefinition, UpdatePlayerDefinitionBody } from "../types/PlayerDefinition";

/**
 * Fetch the games that can be joined, with each active slot flagged as claimed or free.
 * Requires a user token (the http interceptor attaches it); the account is read server-side.
 */
export async function getMatchMakingGames(): Promise<MatchMakingGamesResponse> {
  const { data } = await http.get<MatchMakingGamesResponse>("/match-making/games");
  return data;
}

/**
 * Claim an unassigned player slot for the logged-in user. Resolves on success (HTTP 204) and
 * rejects with a readable message on failure (e.g. the slot was taken, or the user already has
 * a slot in that game).
 */
export async function claimPlayerSlot(gameId: number, playerId: number): Promise<void> {
  await http.post("/match-making/claim", { gameId, playerId });
}

/**
 * Read back the caller's own ruler and faction identity in one game, plus the colours their rivals hold.
 *
 * The route names the game, never the player — the server resolves the slot from the token — so there is no player id
 * to pass and no way to ask about someone else's seat. Rejects with a 404 when the caller holds no slot in that game.
 */
export async function getPlayerDefinition(gameId: number): Promise<PlayerDefinition> {
  const { data } = await http.get<PlayerDefinition>(`/match-making/games/${gameId}/my-player`);
  return data;
}

/**
 * Save the caller's ruler and faction identity. A full replacement of every value, not a patch.
 *
 * Resolves on success (HTTP 204). Rejects with a 409 when another player in that game has taken the colour since the
 * definition was read — the picker's struck-through swatches are a courtesy, not the guarantee.
 */
export async function updatePlayerDefinition(gameId: number, body: UpdatePlayerDefinitionBody): Promise<void> {
  await http.put(`/match-making/games/${gameId}/my-player`, body);
}
