// Remove in F-18. Temporary developer hot-seat support: ends the current player's turn and hands the browser back a session for whoever plays next, so
// one person can play every seat in a game without knowing anyone else's password. The whole file goes when F-18 is done.

import { http } from "@/api/http";
import type { PermissionType } from "@/features/system/types/Permission";

/**
 * Everything the API hands back after switching player: the new player's tokens, and the game detail the Open Game dialog would normally have supplied.
 *
 * Remove in F-18. Mirrors the API's `DTDevSwitchPlayerResult`, which lives beside the endpoint rather than in the contracts projects for the same reason
 * this file does — it is temporary, and both halves should disappear together.
 */
export type DevSwitchPlayerResult = {
  token: string;
  refreshToken: string;
  username: string;
  gameId: number;
  playerId: number;
  permissions: PermissionType[];

  gameName: string;
  mapWidth: number;
  mapHeight: number;
  /** The new player's ruler label, in the "Title Name (Faction)" shape the Open Game listing uses. */
  rulerLabel: string;
  campaignOffsetId: number;

  /** How many turns were ended to get here: one for a single step, one per player for a whole round. */
  turnsEnded: number;
  /** True when at least one of those turns closed the round. */
  roundEnded: boolean;
};

/**
 * Ends the current player's campaign turn and returns the session to continue as whoever plays next.
 *
 * Remove in F-18. Nothing identifies the player: the API reads the game and player from the game token, exactly as the ordinary end-turn endpoint does.
 * The turn is always force-ended — the check-first mode exists to ask a player whether they have finished, and pressing a developer button has already
 * answered that.
 *
 * With `cycleWholeRound` the turns of every player in between are ended too, stopping once it is the caller's turn again. Those players are never logged
 * in; only the player the caller lands on is.
 */
export async function postEndTurnAndSwitchPlayer(cycleWholeRound: boolean): Promise<DevSwitchPlayerResult> {
  const { data } = await http.post<DevSwitchPlayerResult>("/dev/end-turn-and-switch-player", null, { params: { cycleWholeRound } });

  return data;
}
