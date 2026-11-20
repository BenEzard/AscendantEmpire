// API service for taking a map resource off a tile: the payoff behind the Take Resources toolbar action.
//
// Two calls. The options call says which map resources the selection could take from and what each would give them — a tile
// holds up to two, so how many there are decides whether the player is asked to choose at all. The take call performs it,
// immediately and at the cost of each taking force's whole turn.
//
// The game and player come from the game token, so nothing player-scoped is sent — and neither is the tile, because the
// resource is taken where the forces already stand.

import { http } from "@/api/http";

/** One asset a take would put, or did put, into cargo. */
export interface TakeResourcesAssetLine {
  assetId: number;
  assetDisplayName: string;
  /** Whole units, rounded down, the way the asset ticker reports an amount. */
  amount: number;
}

/** One map resource on the tile the selection could take from. */
export interface TakeResourcesOption {
  mapResourceInstanceId: number;
  /** Which of the tile's two places it holds, so the dialog lists them as the tile draws them. */
  slot: number;
  displayName: string;
  iconPath: string | null;
  /**
   * How many whole units the selection would take, bounded by what is actually there.
   *
   * What the forces would get, not what the tile holds: a player only ever sees their own fuzzed estimate of the real
   * quantity, so nothing on the wire discloses it. Nothing shows this figure today — the choice the dialog offers is between
   * resources, and the amount is the same arithmetic whichever one is picked.
   */
  unitsWouldTake: number;
  /** What that many units becomes in cargo — a line per asset the player's switches leave on. */
  wouldGain: TakeResourcesAssetLine[];
}

/** What the caller needs in order to decide whether there is a choice to offer, and to offer it. */
export interface TakeResourcesOptions {
  /** In tile slot order. Empty where nothing is takeable, which means the tile changed since the toolbar judged it. */
  resources: TakeResourcesOption[];
}

export interface TakeResourcesRequest {
  /**
   * The forces taking. Read as force ids: unit instance ids today, and command ids too once commands are fielded.
   *
   * The whole selection, unlike a split or a founding: there is one resource and one rate, so nothing about the order is per
   * force. They take in ascending id against the dwindling quantity, so a later force may find nothing left.
   */
  forceIds: number[];
  /** Which of the tile's map resources to take from. Must be one the options call offered for this selection. */
  mapResourceInstanceId: number;
}

/** What one force in the order came away with. */
export interface TakeResourcesForceOutcome {
  unitInstanceId: number;
  displayName: string;
  /** Zero where the forces ahead of it had already emptied the resource. Such a force kept its movement: it did nothing. */
  unitsTaken: number;
}

/** What the take produced. */
export interface TakeResourcesResult {
  displayName: string;
  xCoord: number;
  yCoord: number;
  unitsTaken: number;
  /** A renewable resource is not gone when it empties — it goes dormant and may come back at the end of a round. */
  isExhausted: boolean;
  /** Summed across the whole selection. */
  gained: TakeResourcesAssetLine[];
  /** In the order they took — ascending force id. */
  forces: TakeResourcesForceOutcome[];
}

/**
 * Get which map resources the selection could take from, and what each would give them.
 *
 * A separate call rather than a payload on the context-actions list, for the same reason the split options are: that list says
 * only whether an action is available, and hanging a per-action payload off it would make every action's contract the union of
 * every action's needs.
 */
export async function getTakeResourcesOptions(forceIds: readonly number[]): Promise<TakeResourcesOptions> {
  // Built by hand rather than through axios's `params` option, which serialises an array as `id[]=1&id[]=2`; the endpoint's
  // [FromQuery(Name = "id")] binds only the repeated `id=1&id=2` form.
  const query = new URLSearchParams(forceIds.map((forceId) => ["id", String(forceId)])).toString();

  const { data } = await http.get<TakeResourcesOptions>(`/force-orders/actions/take-resources/options?${query}`);

  return data;
}

/**
 * Take a map resource into the selection's cargo.
 *
 * A refusal is a 400 whose message is written to be shown to the player as it stands — the resource is gone, the forces have
 * already moved. The caller shows it rather than rewording it.
 */
export async function takeResources(request: TakeResourcesRequest): Promise<TakeResourcesResult> {
  const { data } = await http.post<TakeResourcesResult>("/force-orders/take-resources", request);

  return data;
}
