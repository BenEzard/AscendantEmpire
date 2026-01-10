import type { OpenGamePlayer } from "./OpenGamePlayer";

export type OpenGame = {
  gameId: number;
  gameName: string;

  // optional until the backend sends them again
  gameGuid: string;
  numberOfPlayers?: number;

  mapWidth: number;
  mapHeight: number;
  players: OpenGamePlayer[];
};
