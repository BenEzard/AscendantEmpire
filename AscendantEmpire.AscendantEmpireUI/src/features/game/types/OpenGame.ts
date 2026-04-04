import type { OpenGamePlayer } from "./OpenGamePlayer";

export type OpenGame = {
  gameId: number;
  gameName: string;
  numberOfPlayers?: number;

  mapWidth: number;
  mapHeight: number;
  players: OpenGamePlayer[];
};
