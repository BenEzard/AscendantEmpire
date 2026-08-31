// API service for the context action toolbar: what a selected troop can do right now.
//
// The backend owns the decision — which actions apply, whether each can be taken, and the reasons it can't.
// The frontend owns the presentation (icon, label, and what interaction the button starts); see
// troopActionRegistry.tsx. The game and player come from the game token, so nothing player-scoped is sent.

import { http } from "@/api/http";

/**
 * Which action a toolbar entry is. Mirrors the C# TroopActionType enum by member name — the API registers a
 * JsonStringEnumConverter, so these arrive as strings rather than numbers.
 */
export type TroopActionType = "SplitTroops";

/** One action offered for the selected troop (mirrors ITroopContextAction). */
export interface TroopContextAction {
  actionType: TroopActionType;
  /** The backend's label. Used only as a fallback when the frontend registry doesn't know this action type. */
  label: string;
  /** The action's fixed position in the toolbar. Ascending; stable across requests. */
  displayOrder: number;
  isEnabled: boolean;
  /**
   * Every reason the action is currently unavailable — empty when enabled. Actions the troop could *never*
   * take are absent from the response entirely, so anything here is worth showing even when disabled.
   */
  disabledReasons: string[];
}

/**
 * Get the context actions available to one troop, already in display order.
 *
 * The response is a rendering hint, never an authorisation: the backend re-checks the very same rules when
 * the action is actually executed.
 */
export async function getTroopContextActions(unitInstanceId: number): Promise<TroopContextAction[]> {
  const { data } = await http.get<{ actions: TroopContextAction[] }>(`/troop-orders/units/${unitInstanceId}/actions`);
  return data.actions;
}
