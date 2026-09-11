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

  // An owner the players list does not (yet) know is named by number rather than dropped, since knowing it was somebody's is the point of the disclosure.
  const owner = report.ownerPlayerId == null ? null : (context.factionNameByPlayerId?.get(report.ownerPlayerId) ?? `Player ${report.ownerPlayerId}`);

  const subject = owner == null ? kind : `${owner} ${kind}`;

  return report.healthyTroops == null ? subject : `${subject} (${report.healthyTroops} strong)`;
}

/**
 * Composes the sentence for one report.
 *
 * Every form the design lists is a rendering of the same record:
 * - one tile, still in view — "Settler seen at (4, 1)."
 * - one tile, walked out of view — "Settler seen at (4, 1), heading north-east."
 * - several tiles — "Settler seen passing through (4, 1), (5, 1)", with a heading when it left.
 * - created in view — "Settler appeared at (4, 1)", and where it went next.
 * - destroyed in view — "…, and was destroyed there." A destroyed force has no heading: it did not leave.
 */
export function describeSightingReport(report: SightingReport, context: SightingDescriptionContext): string {
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
