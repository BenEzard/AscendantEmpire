import type { MatchMakingSlot } from "./MatchMakingSlot";

// Mirrors the API's IMatchMakingGame (camelCased by ASP.NET's JSON serializer).
export type MatchMakingGame = {
  gameId: number;
  gameName: string;
  numberOfPlayers: number;
  mapWidth: number;
  mapHeight: number;
  // False while the lobby is still filling, which takes two steps: every seat claimed, then every player defined. A game
  // can therefore be full and still not started — the slots' `isDefined` is what tells those two waits apart. Entering a
  // game that hasn't started is refused by the API, so the Play button stays out of reach until this flips.
  hasStarted: boolean;
  slots: MatchMakingSlot[];
};
