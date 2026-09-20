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

/**
 * Whose campaign turn it is right now, and in which round.
 *
 * Read for the round number, which is what last-known force markers age against.
 */
export type CurrentCampaignTurn = {
  playerId: number;
  currentRoundNumber: number;
  isLastTurnInRound: boolean;
  factionName: string;
};

type CurrentCampaignTurnWireDto = Partial<CurrentCampaignTurn>;

/**
 * Reads the game's current campaign turn. The API reads the game from the game token.
 *
 * Rejects (a 404) when nobody is taking a turn, which callers should treat as "the round is not known" rather than as a failure worth reporting.
 */
export async function getCurrentCampaignTurn(): Promise<CurrentCampaignTurn> {
  const { data } = await http.get<CurrentCampaignTurnWireDto>("/turn/campaign");

  return {
    playerId: data.playerId ?? 0,
    currentRoundNumber: data.currentRoundNumber ?? 0,
    isLastTurnInRound: data.isLastTurnInRound ?? false,
    factionName: data.factionName ?? "",
  };
}

/**
 * What the player still has outstanding on the open campaign turn, read without touching it.
 *
 * `isYourTurn` is false both for another player's turn and for a game with no turn open at all, and `possibleActions` is empty in either case — the flag is
 * what separates "you are not the one being asked" from "you have nothing left to do", which otherwise read the same.
 */
export type CampaignPossibleActions = {
  isYourTurn: boolean;
  possibleActions: PossibleAction[];
};

type PossibleActionWireDto = Partial<PossibleAction>;

/**
 * Reads the possible actions off the wire, filling in anything the server left out.
 *
 * Shared by the two routes that report them, so an action means the same thing whether it arrived from the end-turn check or from the read-only route.
 */
function mapPossibleActions(actions: PossibleActionWireDto[] | undefined): PossibleAction[] {
  return (actions ?? []).map((action) => ({
    category: action.category ?? "Military",
    description: action.description ?? "",
    // Explicitly null rather than 0 when absent: 0 is a real tile, and a "go to" that silently sent the player to the map's corner would be worse
    // than no "go to" at all.
    xCoord: action.xCoord ?? null,
    yCoord: action.yCoord ?? null,
    id: action.id ?? 0,
    recordTypeId: action.recordTypeId ?? 0,
  }));
}

type CampaignPossibleActionsWireDto = Partial<{
  isYourTurn: boolean;
  possibleActions: PossibleActionWireDto[];
}>;

/**
 * Reads what the player still has outstanding this turn.
 *
 * A read, and only a read — which is the whole reason it exists. `postEndCampaignTurn(false)` reports the same list, but *ends the turn* when it finds
 * nothing outstanding, so it cannot be used to populate a panel as a game opens.
 *
 * Nothing identifies the player: the API reads the game and player from the game token.
 */
export async function getCampaignPossibleActions(): Promise<CampaignPossibleActions> {
  const { data } = await http.get<CampaignPossibleActionsWireDto>("/turn/possible-actions");

  return {
    // Absent means no: a response the client cannot read should leave the panel empty rather than claim the turn.
    isYourTurn: data.isYourTurn ?? false,
    possibleActions: mapPossibleActions(data.possibleActions),
  };
}

type EndCampaignTurnWireDto = Partial<{
  turnEnded: boolean;
  roundEnded: boolean;
  victoryAchievedByPlayerId: number | null;
  possibleActions: PossibleActionWireDto[];
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
    possibleActions: mapPossibleActions(data.possibleActions),
  };
}
