// API service for founding a settlement: the payoff behind the Create Village and Establish Camp toolbar actions.
//
// The force is consumed immediately and its people become the settlement's population; the settlement itself goes up over the
// next turn or two. The game and player come from the game token, so nothing player-scoped is sent — and neither is the tile,
// because the settlement is founded where the force is standing.

import { http } from "@/api/http";

/**
 * Which tier of settlement to found. Mirrors the C# SettlementTier enum by member name — the API registers a
 * JsonStringEnumConverter, so these go over the wire as strings rather than numbers.
 *
 * Only the two below can be founded; the rest of the ladder (Hamlet, Town, City, MajorCity) is reached by growth.
 */
export type FoundableSettlementTier = "Camp" | "Village";

/** Whether the player names a settlement of this tier, mirroring `SettlementTierRules.IsNamed`. */
export function isNamedTier(tier: FoundableSettlementTier): boolean {
  return tier === "Village";
}

export interface SettleForceRequest {
  unitInstanceId: number;
  tier: FoundableSettlementTier;
  /**
   * What to call the settlement. Omitted for a tier that carries no name.
   *
   * A camp is one: it is a place the settlers stopped rather than one they founded, so the player is never asked. The server
   * decides which tiers are named and drops anything sent for one that is not, so this is a convenience, not the rule.
   */
  name?: string;
}

/** What the founding produced. */
export interface SettleForceResult {
  settlementId: number;
  structureInstanceId: number;
  /** The name as stored — trimmed, so it may differ from what was sent — or null for a tier that carries no name. */
  name: string | null;
  tier: FoundableSettlementTier;
  population: number;
  xCoord: number;
  yCoord: number;
  /** Always 0 straight after founding: founding starts construction rather than finishing it. */
  completionPercent: number;
  /** How many more of the player's turns construction will take. */
  turnsToComplete: number;
}

/**
 * Found a settlement, consuming the force.
 *
 * A refusal is a 400 whose message is written to be shown to the player as it stands — the name is taken, the force has no
 * movement left, the ground cannot be settled. The caller shows it rather than rewording it.
 */
export async function settleForce(request: SettleForceRequest): Promise<SettleForceResult> {
  const { data } = await http.post<SettleForceResult>("/force-orders/settle", request);

  return data;
}
