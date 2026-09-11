import { http } from "@/api/http";
import type { MoveDirection } from "@/features/game/api/forceMovementApi";

/**
 * How much a sighting made out, from "something passed" up to full detail.
 *
 * The `SightingFidelity` enum's member names — the API serialises enums as strings. Ordered, so a higher level always discloses everything a lower one does.
 */
export type SightingFidelity = "Movement" | "Type" | "Owner" | "Detail";

/** What a report is about. One member per producer, not one per sentence — the sentence is composed here, from the fields. */
export type PlayerReportType = "ForceSighted";

/** What a player last knew to be true of a force they watched. A force watched dying is remembered as destroyed, never forgotten. */
export type KnownForceStatus = "Sighted" | "Destroyed";

/** The API's `RecordType` member names, for the observer that picked a force up. */
export type WitnessRecordType = "None" | "Unit" | "Command" | "Structure";

export interface MapCoord {
  x: number;
  y: number;
}

/**
 * One entry in the player's intelligence feed, already stripped by the server to the fidelity its sighting earned.
 *
 * The four subject fields are null whenever the fidelity did not earn them, so null means "not made out" — never "not applicable" and never "zero".
 * See "Reports" and "Fidelity" in docs/design/intelligence-and-sightings.md.
 */
export interface SightingReport {
  reportId: number;
  reportType: PlayerReportType;
  /** The turn the watched events happened in — somebody else's, never the reader's. */
  raisedDuringPlayerTurnId: number;
  roundNumber: number;
  fidelity: SightingFidelity;
  /** The tiles the force was seen on, in order, repeats collapsed. Never empty once normalised: the first is where it was picked up, the last where it was last seen. */
  path: MapCoord[];
  /** The direction it was seen leaving in, or null when it never left view. */
  exitDirection: MoveDirection | null;
  stillVisible: boolean;
  appearedInView: boolean;
  destroyedInView: boolean;
  firstTurnSequence: number;
  lastTurnSequence: number;
  /** The reader's own (or an ally's) observer that picked the force up. */
  firstWitnessId: number;
  firstWitnessRecordType: WitnessRecordType;
  firstWitnessOwnerPlayerId: number;
  /** What kind of force — disclosed from Type. */
  unitTemplateId: number | null;
  /** Whose force — disclosed from Owner. */
  ownerPlayerId: number | null;
  /** Which force, stable across sightings — disclosed from Owner. */
  unitInstanceId: number | null;
  /** Its unwounded soldiers — disclosed only at Detail. */
  healthyTroops: number | null;
}

/**
 * Where the player last saw a force, stripped by the server to the fidelity of that sighting.
 *
 * Deliberately carries no row id. A stable id would tie two sightings of one force together even where fidelity had not earned that; `unitInstanceId` does so only
 * once it has been earned.
 */
export interface KnownForce {
  status: KnownForceStatus;
  fidelity: SightingFidelity;
  lastSeenLocation: MapCoord;
  lastSeenPlayerTurnId: number;
  /** The round the force was last seen in, which the map marker ages from. There is no server-side expiry. */
  lastSeenRoundNumber: number;
  lastSeenTurnSequence: number;
  exitDirection: MoveDirection | null;
  stillVisible: boolean;
  unitTemplateId: number | null;
  ownerPlayerId: number | null;
  unitInstanceId: number | null;
  healthyTroops: number | null;
}

type MapCoordWireDto = Partial<MapCoord>;

type SightingReportWireDto = Partial<Omit<SightingReport, "path" | "exitDirection" | "unitTemplateId" | "ownerPlayerId" | "unitInstanceId" | "healthyTroops">> & {
  path?: MapCoordWireDto[] | null;
  exitDirection?: MoveDirection | null;
  unitTemplateId?: number | null;
  ownerPlayerId?: number | null;
  unitInstanceId?: number | null;
  healthyTroops?: number | null;
};

type KnownForceWireDto = Partial<Omit<KnownForce, "lastSeenLocation" | "exitDirection" | "unitTemplateId" | "ownerPlayerId" | "unitInstanceId" | "healthyTroops">> & {
  lastSeenLocation?: MapCoordWireDto | null;
  exitDirection?: MoveDirection | null;
  unitTemplateId?: number | null;
  ownerPlayerId?: number | null;
  unitInstanceId?: number | null;
  healthyTroops?: number | null;
};

type SightingReportFeedWireDto = Partial<{ reports: SightingReportWireDto[] }>;

type KnownForceCollectionWireDto = Partial<{ knownForces: KnownForceWireDto[] }>;

const SIGHTING_FIDELITIES: readonly SightingFidelity[] = ["Movement", "Type", "Owner", "Detail"];

/**
 * Reads a fidelity off the wire, treating anything unrecognised as the lowest level.
 *
 * Fails closed, the same way the server's redaction does: a level the client does not understand must never be read as licence to show more.
 */
export function normalizeFidelity(fidelity: string | null | undefined): SightingFidelity {
  return SIGHTING_FIDELITIES.includes(fidelity as SightingFidelity) ? (fidelity as SightingFidelity) : "Movement";
}

/**
 * Reads a coordinate off the wire, or null when either half is missing.
 *
 * Null rather than (0, 0): tile (0, 0) is real ground, and a report quietly pinned to the map's corner would be worse than a report that is dropped.
 */
function normalizeCoord(coord: MapCoordWireDto | null | undefined): MapCoord | null {
  if (coord?.x == null || coord?.y == null) {
    return null;
  }

  return { x: coord.x, y: coord.y };
}

/**
 * Normalises one report, or returns null when it has no usable path.
 *
 * A report is a statement about where something was, so one with nowhere to put it cannot be shown at all.
 */
export function normalizeSightingReport(report: SightingReportWireDto): SightingReport | null {
  const path = (report.path ?? []).map(normalizeCoord).filter((coord): coord is MapCoord => coord != null);

  if (path.length === 0) {
    return null;
  }

  return {
    reportId: report.reportId ?? 0,
    reportType: report.reportType ?? "ForceSighted",
    raisedDuringPlayerTurnId: report.raisedDuringPlayerTurnId ?? 0,
    roundNumber: report.roundNumber ?? 0,
    fidelity: normalizeFidelity(report.fidelity),
    path,
    exitDirection: report.exitDirection ?? null,
    stillVisible: report.stillVisible ?? false,
    appearedInView: report.appearedInView ?? false,
    destroyedInView: report.destroyedInView ?? false,
    firstTurnSequence: report.firstTurnSequence ?? 0,
    lastTurnSequence: report.lastTurnSequence ?? 0,
    firstWitnessId: report.firstWitnessId ?? 0,
    firstWitnessRecordType: report.firstWitnessRecordType ?? "None",
    firstWitnessOwnerPlayerId: report.firstWitnessOwnerPlayerId ?? 0,
    // Null, never a default: an absent subject field is the server withholding it, and any guessed value would be exactly the leak redaction prevents.
    unitTemplateId: report.unitTemplateId ?? null,
    ownerPlayerId: report.ownerPlayerId ?? null,
    unitInstanceId: report.unitInstanceId ?? null,
    healthyTroops: report.healthyTroops ?? null,
  };
}

/** Normalises one known force, or returns null when it has nowhere to be drawn. */
export function normalizeKnownForce(knownForce: KnownForceWireDto): KnownForce | null {
  const lastSeenLocation = normalizeCoord(knownForce.lastSeenLocation);

  if (lastSeenLocation == null) {
    return null;
  }

  return {
    status: knownForce.status ?? "Sighted",
    fidelity: normalizeFidelity(knownForce.fidelity),
    lastSeenLocation,
    lastSeenPlayerTurnId: knownForce.lastSeenPlayerTurnId ?? 0,
    lastSeenRoundNumber: knownForce.lastSeenRoundNumber ?? 0,
    lastSeenTurnSequence: knownForce.lastSeenTurnSequence ?? 0,
    exitDirection: knownForce.exitDirection ?? null,
    stillVisible: knownForce.stillVisible ?? false,
    unitTemplateId: knownForce.unitTemplateId ?? null,
    ownerPlayerId: knownForce.ownerPlayerId ?? null,
    unitInstanceId: knownForce.unitInstanceId ?? null,
    healthyTroops: knownForce.healthyTroops ?? null,
  };
}

/**
 * The player's intelligence feed: every report raised for them since their last turn ended, oldest first.
 *
 * No game or player is sent — the server reads both from the game token, so nobody can ask for another player's intelligence.
 */
export async function getSightingReports(): Promise<SightingReport[]> {
  const { data } = await http.get<SightingReportFeedWireDto>("/intelligence/reports");

  return (data.reports ?? []).map(normalizeSightingReport).filter((report): report is SightingReport => report != null);
}

/** Every force the player has watched, as they last saw it, oldest sighting first. Scoped to the game token like the feed. */
export async function getKnownForces(): Promise<KnownForce[]> {
  const { data } = await http.get<KnownForceCollectionWireDto>("/intelligence/known-forces");

  return (data.knownForces ?? []).map(normalizeKnownForce).filter((knownForce): knownForce is KnownForce => knownForce != null);
}
