import type { MoveDirection } from "@/features/game/api/forceMovementApi";
import type { MapCoord, SightingReport } from "./api/intelligenceApi";

/**
 * What the sentence needs beyond the report itself: names for the ids it carries.
 *
 * Looked up at read time rather than stored on the report, so a report reads correctly after a faction renames itself — and, once the relations model lands,
 * after "Foreigner" becomes "Ally".
 */
export interface SightingDescriptionContext {
  /** Unit template names, as /units/display gives them. */
  unitNameByUnitTemplateId?: ReadonlyMap<number, string>;
  /** Faction names by player id, from the game's players list. */
  factionNameByPlayerId?: ReadonlyMap<number, string>;
  /** Structure kind names, as /structures/display gives them. */
  structureNameByStructureId?: ReadonlyMap<number, string>;
}

const DIRECTION_WORDS: Readonly<Record<MoveDirection, string>> = {
  North: "north",
  NorthEast: "north-east",
  East: "east",
  SouthEast: "south-east",
  South: "south",
  SouthWest: "south-west",
  West: "west",
  NorthWest: "north-west",
};

export function formatCoord(coord: MapCoord): string {
  return `(${coord.x}, ${coord.y})`;
}

/**
 * Names a player, or returns null when the report did not disclose one.
 *
 * A player the players list does not (yet) know is named by number rather than dropped, since knowing it was somebody's is the point of the disclosure.
 */
function nameOfPlayer(playerId: number | null, context: SightingDescriptionContext): string | null {
  if (playerId == null) {
    return null;
  }

  return context.factionNameByPlayerId?.get(playerId) ?? `Player ${playerId}`;
}

/** "a Watchtower", "an Outpost". */
function withIndefiniteArticle(noun: string): string {
  return /^[aeiou]/i.test(noun) ? `an ${noun}` : `a ${noun}`;
}

function capitalise(text: string): string {
  return text.charAt(0).toUpperCase() + text.slice(1);
}

/**
 * Names what was seen, using only what the server disclosed.
 *
 * Built from which subject fields are present rather than from the fidelity level, so the wording can never say more than the data does: "Something" when nothing
 * was made out, "Settler" once its kind was, "Red Empire Settler" once its owner was, and its strength at full detail.
 */
export function describeSubject(report: Pick<SightingReport, "unitTemplateId" | "ownerPlayerId" | "healthyTroops">, context: SightingDescriptionContext): string {
  if (report.unitTemplateId == null) {
    return "Something";
  }

  // A template the display lookup does not know is still a force of some kind; saying so beats printing an id.
  const kind = context.unitNameByUnitTemplateId?.get(report.unitTemplateId) ?? "Force";

  const owner = nameOfPlayer(report.ownerPlayerId, context);

  const subject = owner == null ? kind : `${owner} ${kind}`;

  return report.healthyTroops == null ? subject : `${subject} (${report.healthyTroops} strong)`;
}

/**
 * Composes the sentence for one report.
 *
 * A structure report reads quite differently from a force report and is handed to `describeStructureReport`; a combat report to `describeCombatReport`. Every
 * form the design lists for a force is a rendering of the same record:
 * - one tile, still in view — "Settler seen at (4, 1)."
 * - one tile, walked out of view — "Settler seen at (4, 1), heading north-east."
 * - several tiles — "Settler seen passing through (4, 1), (5, 1)", with a heading when it left.
 * - created in view — "Settler appeared at (4, 1)", and where it went next.
 * - destroyed in view — "…, and was destroyed there." A destroyed force has no heading: it did not leave.
 */
export function describeSightingReport(report: SightingReport, context: SightingDescriptionContext): string {
  if (report.reportType === "StructureSighted") {
    return describeStructureReport(report, context);
  }

  if (report.reportType === "CombatSighted") {
    return describeCombatReport(report, context);
  }

  const subject = describeSubject(report, context);
  const [firstTile, ...laterTiles] = report.path;
  const lastTile = report.path[report.path.length - 1];

  let sentence: string;

  if (report.appearedInView) {
    sentence = `${subject} appeared at ${formatCoord(firstTile)}`;

    if (laterTiles.length > 0) {
      sentence += ` and moved through ${laterTiles.map(formatCoord).join(", ")}`;
    }
  } else if (report.path.length === 1) {
    sentence = `${subject} seen at ${formatCoord(firstTile)}`;
  } else {
    sentence = `${subject} seen passing through ${report.path.map(formatCoord).join(", ")}`;
  }

  if (report.destroyedInView) {
    sentence += report.path.length === 1 ? ", and was destroyed there" : `, and was destroyed at ${formatCoord(lastTile)}`;
  } else if (report.exitDirection != null) {
    sentence += `, heading ${DIRECTION_WORDS[report.exitDirection]}`;
  }

  return `${sentence}.`;
}

/**
 * Composes the sentence for a structure report: everything the reader saw happen to one structure in one turn, in the order it can happen.
 *
 * - construction — "Red Empire started construction on a Watchtower at (4, 1)." A structure seen both founded and finished in the same turn reads only as
 *   "completed construction": the start is not news once the finish is in the same sentence. Progress between the two is never reported.
 * - capture — "Red Empire captured Blue Empire's Watchtower at (4, 1)."
 * - razing — "Blue Empire's Watchtower at (4, 1) was razed."
 * - together — "Red Empire started construction on a Watchtower at (4, 1), then razed it."
 *
 * As with a force, the wording is built from which fields were disclosed: a structure made out only as movement is "a structure", with nobody named as having
 * done anything to it.
 */
export function describeStructureReport(report: SightingReport, context: SightingDescriptionContext): string {
  // A kind the display lookup does not know is still a structure of some kind, named as such rather than by id.
  const kind = report.structureId == null ? "structure" : (context.structureNameByStructureId?.get(report.structureId) ?? "Structure");

  // A structure never moves, so its path is its one tile.
  const at = formatCoord(report.path[0]);

  // The owner at the last event seen: the builder, or the captor once a capture was seen.
  const owner = nameOfPlayer(report.ownerPlayerId, context);
  const previousOwner = nameOfPlayer(report.previousOwnerPlayerId, context);

  let sentence: string | null = null;

  // Whether the sentence so far names who acted, so a razing that follows can say "then razed it" rather than "then it was razed".
  let namesWhoActed = false;

  if (report.completedInView) {
    sentence = owner != null ? `${owner} completed construction on ${withIndefiniteArticle(kind)} at ${at}` : `Construction was completed on ${withIndefiniteArticle(kind)} at ${at}`;
    namesWhoActed = owner != null;
  } else if (report.appearedInView) {
    sentence = owner != null ? `${owner} started construction on ${withIndefiniteArticle(kind)} at ${at}` : `Construction was started on ${withIndefiniteArticle(kind)} at ${at}`;
    namesWhoActed = owner != null;
  }

  if (report.capturedInView) {
    if (sentence == null) {
      if (owner == null) {
        sentence = `${capitalise(withIndefiniteArticle(kind))} at ${at} changed hands`;
      } else {
        sentence = previousOwner != null ? `${owner} captured ${previousOwner}'s ${kind} at ${at}` : `${owner} captured ${withIndefiniteArticle(kind)} at ${at}`;
      }
    } else {
      sentence += owner != null ? `, then ${owner} captured it` : ", then it changed hands";
    }

    namesWhoActed = owner != null;
  }

  if (report.destroyedInView) {
    if (sentence == null) {
      sentence = owner != null ? `${owner}'s ${kind} at ${at} was razed` : `${capitalise(withIndefiniteArticle(kind))} at ${at} was razed`;
    } else {
      sentence += namesWhoActed ? ", then razed it" : ", then it was razed";
    }
  }

  // No reported event was flagged. The server never raises a structure report for construction progress alone, so this only guards against a malformed one.
  if (sentence == null) {
    sentence = `${capitalise(withIndefiniteArticle(kind))} seen at ${at}`;
  }

  return `${sentence}.`;
}

/**
 * Composes the sentence for a combat report: everything the reader watched happen at one engagement in one turn, in the order it can happen.
 *
 * Only what a campaign-map watcher could perceive, never the battle-map blow-by-blow — that fighting broke out here, that a side arrived or gave way, that it
 * ended, and whose banners were left on the ground:
 * - broke out — "Fighting broke out at (4, 1)."
 * - joined into — "Fighting seen at (4, 1), and more forces joined."
 * - a withdrawal — "Fighting broke out at (4, 1), and a force withdrew."
 * - ended — "Fighting broke out at (4, 1), and ended with Red Empire holding the ground."
 * - still going — "Fighting seen at (4, 1), and was still going on."
 *
 * As with the other two, the wording is built from which fields were disclosed rather than from the fidelity level, so the same engagement reads as "the
 * fighting ended" to a distant watcher and "ended with Red Empire holding the ground, with around 120 troops lost" to a close one.
 */
export function describeCombatReport(report: SightingReport, context: SightingDescriptionContext): string {
  // An engagement is fought over one tile, so its path is that tile.
  const at = formatCoord(report.path[0]);

  const groundHolder = nameOfPlayer(report.groundHeldByPlayerId, context);

  let sentence = report.appearedInView ? `Fighting broke out at ${at}` : `Fighting seen at ${at}`;

  if (report.sideJoinedInView) {
    sentence += ", and more forces joined";
  }

  // A withdrawal is only worth its own clause while the fighting went on; once it has ended, every side left, and saying so twice adds nothing.
  if (report.sideLeftInView && !report.destroyedInView) {
    sentence += ", and a force withdrew";
  }

  if (report.destroyedInView) {
    sentence += groundHolder != null ? `, and ended with ${groundHolder} holding the ground` : ", and the fighting ended";
  } else if (report.stillVisible) {
    sentence += ", and was still going on";
  }

  // Approximate on purpose: this is what a watcher on the next hill made out, not a casualty return.
  if (report.troopsLost != null) {
    sentence += `, with around ${report.troopsLost} troops lost`;
  }

  return `${sentence}.`;
}
