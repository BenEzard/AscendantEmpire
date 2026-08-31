// API service for the context action toolbar: what a selected force can do right now.
//
// The backend owns the decision — which actions apply, whether each can be taken, and the reasons it can't.
// The frontend owns the presentation (icon, label, and what interaction the button starts); see
// forceActionRegistry.tsx. The game and player come from the game token, so nothing player-scoped is sent.

import { http } from "@/api/http";

/**
 * Which action a toolbar entry is. Mirrors the C# ForceActionType enum by member name — the API registers a
 * JsonStringEnumConverter, so these arrive as strings rather than numbers.
 */
export type ForceActionType = "SplitForce";

/** One action offered for the selected force (mirrors IForceContextAction). */
export interface ForceContextAction {
  actionType: ForceActionType;
  /** The backend's label. Used only as a fallback when the frontend registry doesn't know this action type. */
  label: string;
  /** The action's fixed position in the toolbar. Ascending; stable across requests. */
  displayOrder: number;
  isEnabled: boolean;
  /**
   * Every reason the action is currently unavailable — empty when enabled. Actions the force could *never*
   * take are absent from the response entirely, so anything here is worth showing even when disabled.
   */
  disabledReasons: string[];
}

/**
 * Get the context actions available to one force, already in display order.
 *
 * The response is a rendering hint, never an authorisation: the backend re-checks the very same rules when
 * the action is actually executed.
 */
export async function getForceContextActions(unitInstanceId: number): Promise<ForceContextAction[]> {
  const { data } = await http.get<{ actions: ForceContextAction[] }>(`/force-orders/units/${unitInstanceId}/actions`);
  return data.actions;
}
