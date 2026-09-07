/**
 * One of the caller's own in-progress games, as listed by the Open Game dialog.
 *
 * One row per game, not per player. Every game here is one the logged-in account holds an active
 * player slot in — the server scopes the listing to the token, so there is nothing to filter client-side.
 */
export type MyGame = {
  gameId: number;
  gameName: string;

  mapWidth: number;
  mapHeight: number;

  // The caller's own slot in this game. Needed once the game is entered, to draw the board.
  yourPlayerId: number;
  yourRulerLabel: string;
  yourCampaignOffsetId: number;

  // Whoever the campaign queue is currently waiting on, which may well be somebody else.
  // Null is a normal state: a game between turns, or one whose campaign lane has not started, has
  // nobody taking a turn, and the dialog shows a placeholder rather than a ruler.
  currentTurnPlayerId: number | null;
  currentTurnRulerLabel: string | null;
  isYourTurn: boolean;
};
