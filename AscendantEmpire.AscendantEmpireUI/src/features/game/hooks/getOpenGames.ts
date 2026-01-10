import { http } from "../../../api/http";
import type { OpenGamesResponse } from "../types/OpenGamesResponse";

type LoadOpenGameRequest = {
  gameGuid?: string | null;
  playerGuid?: string | null;
};

export async function getOpenGames(): Promise<OpenGamesResponse> {
  const payload: LoadOpenGameRequest = { gameGuid: null, playerGuid: null };

  const res = await http.post<OpenGamesResponse>("/games/open", payload, { headers: { "Content-Type": "application/json" } });

  return res.data;
}
