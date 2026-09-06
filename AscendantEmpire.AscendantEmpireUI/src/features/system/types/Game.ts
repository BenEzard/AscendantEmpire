// Client-side model for a game as listed by the developer System tool's Games page.
//
// Mirrors the backend GetGames contract (AscendantEmpire.FrontendContracts.Games.GetGames.IGameSummary) and the
// GameCreationStatus / GamePlayStatus domain enums (AscendantEmpire.Domain.Enums.Games).
//
// IMPORTANT: the API serializes enums as their NAME, not their numeric value — AscendantEmpire.Api/Program.cs
// registers a JsonStringEnumConverter. Both statuses are therefore modelled as string unions whose members match
// the C# ones exactly, the same convention as InactiveReasonType in ./User.ts.

/** Whether a game's data finished being built. Names match the C# `GameCreationStatus` enum exactly. */
export type GameCreationStatus = "SetupPending" | "SetupInProgress" | "SetupComplete" | "SetupFailed";

/** Where a game has got to in its lifecycle. Names match the C# `GamePlayStatus` enum exactly. */
export type GamePlayStatus = "SetupInProgress" | "AwaitingPlayers" | "InProgress" | "Completed" | "Abandoned";

/**
 * A single game as returned by GET /games. Field names are camelCase to match the JSON the API emits.
 *
 * Unlike the matchmaker's listing this one is unfiltered — games that failed setup or were abandoned appear too,
 * because those are the ones most likely to be here to be deleted.
 */
export interface GameSummary {
  gameId: number;
  gameName: string;
  /** The total number of player slots the game was set up with. */
  numberOfPlayers: number;
  /** How many of those slots a user has actually claimed. */
  claimedPlayerCount: number;
  creationStatus: GameCreationStatus;
  playStatus: GamePlayStatus;
  createdAt: string;
}

/** Human-readable label for each setup status. */
export const GAME_CREATION_STATUS_LABELS: Record<GameCreationStatus, string> = {
  SetupPending: "Setup pending",
  SetupInProgress: "Setup in progress",
  SetupComplete: "Setup complete",
  SetupFailed: "Setup failed",
};

/** Human-readable label for each play status. */
export const GAME_PLAY_STATUS_LABELS: Record<GamePlayStatus, string> = {
  SetupInProgress: "Setup in progress",
  AwaitingPlayers: "Awaiting players",
  InProgress: "In progress",
  Completed: "Completed",
  Abandoned: "Abandoned",
};
