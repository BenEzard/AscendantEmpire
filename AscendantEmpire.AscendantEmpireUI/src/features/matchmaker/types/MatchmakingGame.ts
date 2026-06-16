import type { MatchmakingSlot } from "./MatchmakingSlot";

// Mirrors the API's IMatchmakingGame (camelCased by ASP.NET's JSON serializer).
export type MatchmakingGame = {
  gameId: number;
  gameName: string;
  numberOfPlayers: number;
  mapWidth: number;
  mapHeight: number;
  slots: MatchmakingSlot[];
};
