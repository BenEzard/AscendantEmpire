import { http } from "@/api/http";

/**
 * The broad area of the game an outstanding action belongs to.
 *
 * These are the `InformationCategory` enum's member names — the API serialises enums as strings.
 */
export type InformationCategory = "Military" | "Technology" | "Diplomacy" | "DomesticManagement";

/**
 * Something the player could still do this turn, reported back when they ask to end it.
 *
 * Not a refusal and not an error: the turn is still theirs, and they end it for real by asking again with `force`.
 */
export type PossibleAction = {
  category: InformationCategory;
  description: string;
  /** The tile the action is on, or null for actions with no place on the map (research, trade). */
  xCoord: number | null;
  yCoord: number | null;
  /** The record the action concerns — a force id for the movement checks. */
  id: number;
  recordTypeId: number;
};

export type EndCampaignTurnOutcome = {
  /** False when the turn is still running because the player has actions outstanding. */
  turnEnded: boolean;
  roundEnded: boolean;
  victoryAchievedByPlayerId: number | null;
  possibleActions: PossibleAction[];
};

type EndCampaignTurnWireDto = Partial<{
  turnEnded: boolean;
  roundEnded: boolean;
  victoryAchievedByPlayerId: number | null;
  possibleActions: Partial<PossibleAction>[];
}>;

/**
 * Asks the API to end the current player's campaign turn.
 *
 * Sent twice in the ordinary case. Without `force` the server checks first: anything the player might still want to do comes back in
 * `possibleActions` with `turnEnded` false, and the turn keeps running. With `force` the turn ends regardless — that is the call to make once the
 * player has seen the list and chosen to end anyway.
 *
 * Nothing identifies the player: the API reads the game and player from the game token, so a turn cannot be ended on someone else's behalf.
 */
export async function postEndCampaignTurn(force: boolean): Promise<EndCampaignTurnOutcome> {
  const { data } = await http.post<EndCampaignTurnWireDto>("/turn/end-campaign", null, { params: { force } });

  return {
    turnEnded: data.turnEnded ?? false,
    roundEnded: data.roundEnded ?? false,
    victoryAchievedByPlayerId: data.victoryAchievedByPlayerId ?? null,
    possibleActions: (data.possibleActions ?? []).map((action) => ({
      category: action.category ?? "Military",
      description: action.description ?? "",
      // Explicitly null rather than 0 when absent: 0 is a real tile, and a "go to" that silently sent the player to the map's corner would be worse
      // than no "go to" at all.
      xCoord: action.xCoord ?? null,
      yCoord: action.yCoord ?? null,
      id: action.id ?? 0,
      recordTypeId: action.recordTypeId ?? 0,
    })),
  };
}
