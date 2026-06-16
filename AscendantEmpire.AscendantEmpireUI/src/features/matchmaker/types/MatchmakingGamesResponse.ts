import type { MatchmakingGame } from "./MatchmakingGame";

// Mirrors the API's IMatchmakingGameCollection.
export type MatchmakingGamesResponse = {
  games: MatchmakingGame[];
};
