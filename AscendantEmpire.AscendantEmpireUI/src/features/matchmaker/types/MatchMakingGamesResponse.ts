import type { MatchMakingGame } from "./MatchMakingGame";

// Mirrors the API's IMatchMakingGameCollection.
export type MatchMakingGamesResponse = {
  games: MatchMakingGame[];
};
