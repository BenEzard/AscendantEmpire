import type { MatchMakingSlot } from "./MatchMakingSlot";

// Mirrors the API's IMatchMakingGame (camelCased by ASP.NET's JSON serializer).
export type MatchMakingGame = {
  gameId: number;
  gameName: string;
  numberOfPlayers: number;
  mapWidth: number;
  mapHeight: number;
  slots: MatchMakingSlot[];
};
