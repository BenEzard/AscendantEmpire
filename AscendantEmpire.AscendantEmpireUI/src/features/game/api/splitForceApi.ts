// API service for splitting a force: the payoff behind the Split Force toolbar action.
//
// Two calls. The options call tells the dialog what a legal division of this force looks like — chiefly its organisation tag's
// authored base quantum, which every group's size is a multiple of. The split call performs the division, immediately and at
// the cost of the force's whole turn.
//
// The game and player come from the game token, so nothing player-scoped is sent — and neither is the tile, because the groups
// stand where the force already stood.

import { http } from "@/api/http";

/** What a legal division of one unled force looks like, as the dialog opens. */
export interface SplitForceOptions {
  unitInstanceId: number;
  /** The force's name, for the dialog's title. */
  displayName: string;
  /**
   * How the force's organisation is led, for the dialog's title.
   *
   * Always "Unled" while the options endpoint serves unled forces alone — sent rather than assumed so the title stays honest
   * when the commissioned branch of splitting is built.
   */
  leadershipModel: "Unled" | "Commissioned";
  healthyCount: number;
  /** The wounded travel with their group, so they count towards the strength being divided. */
  injuredCount: number;
  /** Healthy and injured together — the figure the split rules measure. */
  totalStrength: number;
  /**
   * The organisation tag's authored base quantum: the smallest group this force divides into.
   *
   * Always greater than zero. An unled tag authored without one is a configuration error, and the endpoint refuses rather than
   * letting the dialog offer splits the server would then reject.
   */
  baseSize: number;
}

export interface SplitForceRequest {
  unitInstanceId: number;
  /**
   * The strength of each group to produce, in the order the player composed them.
   *
   * The first group is the one the original force keeps; the rest are created beside it. Casualty counts are deliberately not
   * sent — the server apportions the wounded in proportion to group size, by the same algorithm the dialog previews with.
   */
  groupSizes: number[];
}

/** One of the forces a completed split produced. */
export interface SplitForceGroup {
  unitInstanceId: number;
  size: number;
  healthyCount: number;
  injuredCount: number;
  /** Exactly one group has this: the force that was split, which survives rather than being destroyed and recreated. */
  isOriginalForce: boolean;
}

/** What the split produced — every force now standing where the one that split was. */
export interface SplitForceResult {
  xCoord: number;
  yCoord: number;
  /** In the order the requested sizes were given, the original first. */
  groups: SplitForceGroup[];
}

/**
 * Get what a legal division of this force looks like.
 *
 * A separate call rather than a payload on the context-actions list: splitting is the one toolbar action that needs parameters,
 * and hanging those off the list endpoint would make every action's contract the union of every action's needs.
 */
export async function getSplitForceOptions(unitInstanceId: number): Promise<SplitForceOptions> {
  const { data } = await http.get<SplitForceOptions>(`/force-orders/actions/split-force/options?id=${unitInstanceId}`);

  return data;
}

/**
 * Split a force into the given group sizes.
 *
 * A refusal is a 400 whose message is written to be shown to the player as it stands — the force has already moved, the sizes
 * are not a legal division. The caller shows it rather than rewording it.
 */
export async function splitForce(request: SplitForceRequest): Promise<SplitForceResult> {
  const { data } = await http.post<SplitForceResult>("/force-orders/split", request);

  return data;
}
