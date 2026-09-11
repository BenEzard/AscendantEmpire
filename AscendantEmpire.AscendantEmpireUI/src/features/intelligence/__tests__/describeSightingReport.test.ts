import type { SightingReport } from "../api/intelligenceApi";
import { describeSightingReport, type SightingDescriptionContext } from "../describeSightingReport";

/** A report about player 1's Settler, seen in full detail at (4, 1) and still in view, unless a test says otherwise. */
function report(overrides: Partial<SightingReport> = {}): SightingReport {
  return {
    reportId: 1,
    reportType: "ForceSighted",
    raisedDuringPlayerTurnId: 13,
    roundNumber: 2,
    fidelity: "Detail",
    path: [{ x: 4, y: 1 }],
    exitDirection: null,
    stillVisible: true,
    appearedInView: false,
    destroyedInView: false,
    firstTurnSequence: 1,
    lastTurnSequence: 1,
    firstWitnessId: 50,
    firstWitnessRecordType: "Unit",
    firstWitnessOwnerPlayerId: 2,
    unitTemplateId: 3,
    ownerPlayerId: 1,
    unitInstanceId: 99,
    healthyTroops: null,
    ...overrides,
  };
}

const CONTEXT: SightingDescriptionContext = {
  unitNameByUnitTemplateId: new Map([[3, "Settler"]]),
  factionNameByPlayerId: new Map([[1, "Red Empire"]]),
};

/**
 * The design's point about reports: the different sentences are renderings of one record, not different report types. These pin each rendering.
 */
describe("describeSightingReport", () => {
  it("describes one tile still in view as seen there", () => {
    expect(describeSightingReport(report(), CONTEXT)).toBe("Red Empire Settler seen at (4, 1).");
  });

  it("adds the heading when the force walked out of view", () => {
    expect(describeSightingReport(report({ stillVisible: false, exitDirection: "NorthEast" }), CONTEXT)).toBe("Red Empire Settler seen at (4, 1), heading north-east.");
  });

  it("lists every tile of a longer track as passed through, in order", () => {
    const passing = report({ path: [{ x: 4, y: 1 }, { x: 5, y: 1 }, { x: 5, y: 2 }], stillVisible: false, exitDirection: "South" });

    expect(describeSightingReport(passing, CONTEXT)).toBe("Red Empire Settler seen passing through (4, 1), (5, 1), (5, 2), heading south.");
  });

  it("says a force created in view appeared, and where it went next", () => {
    const appeared = report({ appearedInView: true, path: [{ x: 4, y: 1 }, { x: 5, y: 1 }] });

    expect(describeSightingReport(appeared, CONTEXT)).toBe("Red Empire Settler appeared at (4, 1) and moved through (5, 1).");
  });

  /** A destroyed force did not go anywhere, so it never has a heading, even if the record carried one. */
  it("says a force was destroyed rather than giving it a heading", () => {
    expect(describeSightingReport(report({ destroyedInView: true, stillVisible: false, exitDirection: "East" }), CONTEXT)).toBe(
      "Red Empire Settler seen at (4, 1), and was destroyed there.",
    );
    expect(describeSightingReport(report({ destroyedInView: true, path: [{ x: 4, y: 1 }, { x: 5, y: 1 }] }), CONTEXT)).toBe(
      "Red Empire Settler seen passing through (4, 1), (5, 1), and was destroyed at (5, 1).",
    );
  });

  /**
   * The wording can never say more than the data: it is built from which fields the server disclosed, so a glimpse reads as "Something" whatever names the
   * client happens to hold.
   */
  it("names only what the sighting disclosed", () => {
    expect(describeSightingReport(report({ unitTemplateId: null, ownerPlayerId: null, unitInstanceId: null }), CONTEXT)).toBe("Something seen at (4, 1).");
    expect(describeSightingReport(report({ ownerPlayerId: null, unitInstanceId: null }), CONTEXT)).toBe("Settler seen at (4, 1).");
    expect(describeSightingReport(report({ healthyTroops: 45 }), CONTEXT)).toBe("Red Empire Settler (45 strong) seen at (4, 1).");
  });

  it("falls back to a generic name for a template or owner the lookups do not know", () => {
    expect(describeSightingReport(report({ unitTemplateId: 77, ownerPlayerId: 9 }), CONTEXT)).toBe("Player 9 Force seen at (4, 1).");
  });
});
