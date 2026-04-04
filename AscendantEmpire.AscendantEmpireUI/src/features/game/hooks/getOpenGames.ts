import { http } from "../../../api/http";
import type { OpenGamesResponse } from "../types/OpenGamesResponse";

type LoadOpenGameRequest = {
  gameId?: number | null;
  playerId?: number | null;
};

export async function getOpenGames(): Promise<OpenGamesResponse> {
  const payload: LoadOpenGameRequest = { gameId: null, playerId: null };

  const res = await http.post<OpenGamesResponse>("/games/open", payload, { headers: { "Content-Type": "application/json" } });

  return res.data;
}
