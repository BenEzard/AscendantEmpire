// API service for the dev System tool's Games page. Thin wrappers over the shared axios `http` client,
// one per backend endpoint on GamesController (GET /games, DELETE /games/{gameId}).
//
// Both endpoints require the DeleteGame permission; the bearer token is attached by the http interceptor.

import { http } from "@/api/http";

import type { GameSummary } from "../types/Game";

/** The envelope GET /games responds with — a collection wrapper, matching IGameSummaryCollection. */
interface GameSummaryCollection {
  games: GameSummary[];
}

/** GET /games — every game, whatever state it is in. */
export async function getGames(): Promise<GameSummary[]> {
  const { data } = await http.get<GameSummaryCollection>("/games");
  return data.games;
}

/**
 * DELETE /games/{gameId} — starts a background purge of the game and returns its processId (bare int, HTTP 201),
 * so the caller can poll GET /system/status for progress.
 *
 * The game is not gone when this resolves; it is gone when that poll reports the job complete.
 */
export async function deleteGame(gameId: number, signal?: AbortSignal): Promise<number> {
  const { data } = await http.delete<number>(`/games/${gameId}`, { signal });
  return data;
}
