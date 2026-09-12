import { http } from "@/api/http";
import type { MoveDirection } from "@/features/game/api/forceMovementApi";

/**
 * How much a sighting made out, from "something passed" up to full detail.
 *
 * The `SightingFidelity` enum's member names — the API serialises enums as strings. Ordered, so a higher level always discloses everything a lower one does.
 */
export type SightingFidelity = "Movement" | "Type" | "Owner" | "Detail";

/** What a report is about. One member per producer, not one per sentence — the sentence is composed here, from the fields. */
export type PlayerReportType = "ForceSighted" | "StructureSighted";

/** What a player last knew to be true of a force they watched. A force watched dying is remembered as destroyed, never forgotten. */
export type KnownForceStatus = "Sighted" | "Destroyed";

/** What a player last knew to be true of a structure they watched change. A structure watched being razed is remembered as razed, never forgotten. */
export type KnownStructureStatus = "Standing" | "Razed";

/** The API's `RecordType` member names, for the observer that picked a force up. */
export type WitnessRecordType = "None" | "Unit" | "Command" | "Structure";

export interface MapCoord {
  x: number;
  y: number;
}

/**
 * One entry in the player's intelligence feed, already stripped by the server to the fidelity its sighting earned.
 *
 * A force report fills the unit subject fields and a structure report the structure ones; the other set is null. Subject fields are also null whenever the
 * fidelity did not earn them, so null means "not made out" or "not this kind of report" — never "zero". See "Reports" and "Fidelity" in
 * docs/design/intelligence-and-sightings.md.
 */
export interface SightingReport {
  reportId: number;
  reportType: PlayerReportType;
  /** The turn the watched events happened in — somebody else's, never the reader's. */
  raisedDuringPlayerTurnId: number;
  roundNumber: number;
  fidelity: SightingFidelity;
  /**
   * The tiles the subject was seen on, in order, repeats collapsed. Never empty once normalised: the first is where it was picked up, the last where it was last
   * seen. A structure never moves, so a structure report's path is its one tile.
   */
  path: MapCoord[];
  /** The direction it was seen leaving in, or null when it never left view. Always null for a structure. */
  exitDirection: MoveDirection | null;
  stillVisible: boolean;
  /** A force seen being created — or, on a structure report, a structure seen being founded. */
  appearedInView: boolean;
  /** A force seen being destroyed — or, on a structure report, a structure seen being razed. */
  destroyedInView: boolean;
  /** On a structure report, whether it was seen being finished. Null on a force report. */
  completedInView: boolean | null;
  /** On a structure report, whether it was seen changing hands. Null on a force report. */
  capturedInView: boolean | null;
  firstTurnSequence: number;
  lastTurnSequence: number;
  /** The reader's own (or an ally's) observer that picked the subject up. */
  firstWitnessId: number;
  firstWitnessRecordType: WitnessRecordType;
  firstWitnessOwnerPlayerId: number;
  /** What kind of force — disclosed from Type. */
  unitTemplateId: number | null;
  /** Whose force or structure — disclosed from Owner. For a structure, who held it at the last event seen. */
  ownerPlayerId: number | null;
  /** Which force, stable across sightings — disclosed from Owner. */
  unitInstanceId: number | null;
  /** Its unwounded soldiers — disclosed only at Detail. */
  healthyTroops: number | null;
  /** Which structure, stable across sightings — disclosed from Owner. */
  structureInstanceId: number | null;
  /** What kind of structure — disclosed from Type. */
  structureId: number | null;
  /** Who held the structure before a capture that was seen — disclosed from Owner. */
  previousOwnerPlayerId: number | null;
  /** How far built the structure was, 0 to 100 — disclosed only at Detail. */
  completionPercent: number | null;
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

/**
 * What the player last saw of a structure — whose it was, how far built, whether it stood — stripped by the server to the fidelity of that sighting.
 *
 * No row id, for the same reason as `KnownForce`.
 */
export interface KnownStructure {
  status: KnownStructureStatus;
  fidelity: SightingFidelity;
  /** The tile it stands, or stood, on. */
  location: MapCoord;
  lastSeenPlayerTurnId: number;
  /** The round it was last seen in, which the map marker ages from. */
  lastSeenRoundNumber: number;
  lastSeenTurnSequence: number;
  stillVisible: boolean;
  /** What kind of structure — disclosed from Type. */
  structureId: number | null;
  /** Whose it was when last seen — disclosed from Owner. */
  ownerPlayerId: number | null;
  /** Which structure — disclosed from Owner. */
  structureInstanceId: number | null;
  /** How far built, 0 to 100 — disclosed only at Detail. */
  completionPercent: number | null;
}

type MapCoordWireDto = Partial<MapCoord>;

/** The fields of a report that are nullable on the wire, kept out of the `Partial` so their `null` survives into the wire type. */
type NullableReportFields =
  | "exitDirection"
  | "completedInView"
  | "capturedInView"
  | "unitTemplateId"
  | "ownerPlayerId"
  | "unitInstanceId"
  | "healthyTroops"
  | "structureInstanceId"
  | "structureId"
  | "previousOwnerPlayerId"
  | "completionPercent";

type SightingReportWireDto = Partial<Omit<SightingReport, "path" | "reportType" | NullableReportFields>> & {
  path?: MapCoordWireDto[] | null;
  /** A string rather than the union: a newer server may send a report type this client does not know yet. */
  reportType?: string | null;
} & { [Field in NullableReportFields]?: SightingReport[Field] };

type KnownForceWireDto = Partial<Omit<KnownForce, "lastSeenLocation" | "exitDirection" | "unitTemplateId" | "ownerPlayerId" | "unitInstanceId" | "healthyTroops">> & {
  lastSeenLocation?: MapCoordWireDto | null;
  exitDirection?: MoveDirection | null;
  unitTemplateId?: number | null;
  ownerPlayerId?: number | null;
  unitInstanceId?: number | null;
  healthyTroops?: number | null;
};

type KnownStructureWireDto = Partial<Omit<KnownStructure, "location" | "structureId" | "ownerPlayerId" | "structureInstanceId" | "completionPercent">> & {
  location?: MapCoordWireDto | null;
  structureId?: number | null;
  ownerPlayerId?: number | null;
  structureInstanceId?: number | null;
  completionPercent?: number | null;
};

type SightingReportFeedWireDto = Partial<{ reports: SightingReportWireDto[] }>;

type KnownForceCollectionWireDto = Partial<{ knownForces: KnownForceWireDto[] }>;

type KnownStructureCollectionWireDto = Partial<{ knownStructures: KnownStructureWireDto[] }>;

const SIGHTING_FIDELITIES: readonly SightingFidelity[] = ["Movement", "Type", "Owner", "Detail"];

const PLAYER_REPORT_TYPES: readonly PlayerReportType[] = ["ForceSighted", "StructureSighted"];

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
 * Normalises one report, or returns null when it cannot be shown.
 *
 * A report is a statement about where something was, so one with nowhere to put it cannot be shown at all. Nor can one of a type this client does not know:
 * describing it as a force report would put a false sentence in the feed. A missing type is still read as a force report, which is all older servers sent.
 */
export function normalizeSightingReport(report: SightingReportWireDto): SightingReport | null {
  const path = (report.path ?? []).map(normalizeCoord).filter((coord): coord is MapCoord => coord != null);

  if (path.length === 0) {
    return null;
  }

  const reportType = report.reportType ?? "ForceSighted";

  if (!PLAYER_REPORT_TYPES.includes(reportType as PlayerReportType)) {
    return null;
  }

  return {
    reportId: report.reportId ?? 0,
    reportType: reportType as PlayerReportType,
    raisedDuringPlayerTurnId: report.raisedDuringPlayerTurnId ?? 0,
    roundNumber: report.roundNumber ?? 0,
    fidelity: normalizeFidelity(report.fidelity),
    path,
    exitDirection: report.exitDirection ?? null,
    stillVisible: report.stillVisible ?? false,
    appearedInView: report.appearedInView ?? false,
    destroyedInView: report.destroyedInView ?? false,
    completedInView: report.completedInView ?? null,
    capturedInView: report.capturedInView ?? null,
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
    structureInstanceId: report.structureInstanceId ?? null,
    structureId: report.structureId ?? null,
    previousOwnerPlayerId: report.previousOwnerPlayerId ?? null,
    completionPercent: report.completionPercent ?? null,
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

/** Normalises one known structure, or returns null when it has nowhere to be drawn. Withheld fields stay null, exactly as for a report. */
export function normalizeKnownStructure(knownStructure: KnownStructureWireDto): KnownStructure | null {
  const location = normalizeCoord(knownStructure.location);

  if (location == null) {
    return null;
  }

  return {
    status: knownStructure.status ?? "Standing",
    fidelity: normalizeFidelity(knownStructure.fidelity),
    location,
    lastSeenPlayerTurnId: knownStructure.lastSeenPlayerTurnId ?? 0,
    lastSeenRoundNumber: knownStructure.lastSeenRoundNumber ?? 0,
    lastSeenTurnSequence: knownStructure.lastSeenTurnSequence ?? 0,
    stillVisible: knownStructure.stillVisible ?? false,
    structureId: knownStructure.structureId ?? null,
    ownerPlayerId: knownStructure.ownerPlayerId ?? null,
    structureInstanceId: knownStructure.structureInstanceId ?? null,
    completionPercent: knownStructure.completionPercent ?? null,
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

/** Every structure the player has watched change, as they last saw it, oldest sighting first. Scoped to the game token like the feed. */
export async function getKnownStructures(): Promise<KnownStructure[]> {
  const { data } = await http.get<KnownStructureCollectionWireDto>("/intelligence/known-structures");

  return (data.knownStructures ?? []).map(normalizeKnownStructure).filter((knownStructure): knownStructure is KnownStructure => knownStructure != null);
}
