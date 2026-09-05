// API service for the context action toolbar: what the selected forces can do right now.
//
// The backend owns the decision — which actions apply, whether each can be taken, and the reasons it can't.
// The frontend owns the presentation (icon, label, and what interaction the button starts); see
// forceActionRegistry.tsx. The game and player come from the game token, so nothing player-scoped is sent.

import { http } from "@/api/http";

/**
 * Which action a toolbar entry is. Mirrors the C# ForceActionType enum by member name — the API registers a
 * JsonStringEnumConverter, so these arrive as strings rather than numbers.
 */
export type ForceActionType = "SplitForce" | "CreateVillage" | "EstablishCamp";

/** One action offered for the selection (mirrors IForceContextAction). */
export interface ForceContextAction {
  actionType: ForceActionType;
  /** The backend's label. Used only as a fallback when the frontend registry doesn't know this action type. */
  label: string;
  /** The action's fixed position in the toolbar. Ascending; stable across requests. */
  displayOrder: number;
  isEnabled: boolean;
  /**
   * Every reason the action is currently unavailable — empty when enabled. Actions *no* selected force could
   * ever take are absent from the response entirely, so anything here is worth showing even when disabled.
   *
   * With several forces selected these are collected across all of them, and each reason names the force it
   * came from, so the player can see which member of the selection is holding the action back.
   */
  disabledReasons: string[];
}

/**
 * Get the context actions available to the current selection, already in display order.
 *
 * An action offered over several forces is a single order covering all of them, so the backend judges each
 * action against every selected force: enabled only when all of them can take it, disabled with the collected
 * reasons when at least one of them could but currently cannot, and omitted when none of them ever could.
 *
 * The response is a rendering hint, never an authorisation: the backend re-checks the very same rules over the
 * very same selection when the action is actually executed.
 */
export async function getForceContextActions(forceIds: readonly number[]): Promise<ForceContextAction[]> {
  // Built by hand rather than through axios's `params` option, which is the obvious thing to reach for and is
  // wrong here: axios serialises an array as `id[]=1&id[]=2`, and the endpoint's [FromQuery(Name = "id")] binds
  // only the repeated `id=1&id=2` form.
  const query = new URLSearchParams(forceIds.map((forceId) => ["id", String(forceId)])).toString();

  const { data } = await http.get<{ actions: ForceContextAction[] }>(`/force-orders/actions?${query}`);
  return data.actions;
}
