import { http } from "@/api/http";
import {
  mergeTilesByCoord,
  normalizeObserver,
  type CampaignMapTileDto,
  type PlayerCampaignMapObserverResultDto,
} from "@/features/map/api/playerCampaignMapApi";

/**
 * The eight directions a force can step in, as the API names them.
 *
 * These are the `MoveDirection` enum's member names — the API serialises enums as strings — and they are map directions rather
 * than screen ones: North is decreasing Y, which draws upward because tile rows run top-down.
 */
export type MoveDirection =
  | "North"
  | "NorthEast"
  | "East"
  | "SouthEast"
  | "South"
  | "SouthWest"
  | "West"
  | "NorthWest";

export type StepForceRequest = {
  unitInstanceId: number;
  direction: MoveDirection;
};

export type StepForceResponse = {
  unitInstanceId: number;
  xCoord: number;
  yCoord: number;
  /** May be negative — a force holding any movement at all may enter a tile, and pays its whole cost. */
  campaignMovementRemaining: number;
  campaignMovementPoints: number;
  /**
   * Whether the step took the force onto a tile held by another player's forces, and so started a battle. When it did, the force
   * and every enemy force on that tile are now in battle and can't take another campaign step until it is over.
   */
  startedBattle: boolean;
  /** Every tile the player can now see, including the one the force just left, which comes back without it. */
  tiles: CampaignMapTileDto[];
  observers: PlayerCampaignMapObserverResultDto[];
};

type StepForceWireDto = Partial<{
  unitInstanceId: number;
  xCoord: number;
  yCoord: number;
  campaignMovementRemaining: number;
  campaignMovementPoints: number;
  startedBattle: boolean;
  observers: Parameters<typeof normalizeObserver>[0][];
}>;

/**
 * Steps one force a single tile and returns where it ended up, what it has left, and the refreshed view of the map.
 *
 * The game and player are not sent: the API reads both from the game token, so a step cannot be aimed at another player's
 * forces. Neither is a destination — the server knows where the force stands and works out the neighbour itself, which is what
 * stops a stale client view from moving a force somewhere it was never standing next to.
 */
export async function postStepForce(req: StepForceRequest): Promise<StepForceResponse> {
  const { data } = await http.post<StepForceWireDto>("/force-orders/step", {
    unitInstanceId: req.unitInstanceId,
    direction: req.direction,
  });

  // The observers come back in the same shape the fog-of-war read uses, so they go through the same normalizer and the tiles
  // can be merged into the cache by the same code.
  const observers = (data.observers ?? []).map(normalizeObserver);

  return {
    unitInstanceId: data.unitInstanceId ?? req.unitInstanceId,
    xCoord: data.xCoord ?? 0,
    yCoord: data.yCoord ?? 0,
    campaignMovementRemaining: data.campaignMovementRemaining ?? 0,
    campaignMovementPoints: data.campaignMovementPoints ?? 0,
    // Missing means an older API that could not start a battle, so it is read as "no battle" rather than guessed at.
    startedBattle: data.startedBattle ?? false,
    // Several observers can see the same tile, so collapse them the way the map read does before anything merges them in.
    tiles: mergeTilesByCoord(observers.flatMap((observer) => observer.seenTiles)),
    observers,
  };
}
