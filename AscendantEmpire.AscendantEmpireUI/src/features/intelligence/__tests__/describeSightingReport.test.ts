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
    completedInView: null,
    capturedInView: null,
    firstTurnSequence: 1,
    lastTurnSequence: 1,
    firstWitnessId: 50,
    firstWitnessRecordType: "Unit",
    firstWitnessOwnerPlayerId: 2,
    unitTemplateId: 3,
    ownerPlayerId: 1,
    unitInstanceId: 99,
    healthyTroops: null,
    structureInstanceId: null,
    structureId: null,
    previousOwnerPlayerId: null,
    completionPercent: null,
    sideJoinedInView: null,
    sideLeftInView: null,
    battleId: null,
    groundHeldByPlayerId: null,
    troopsLost: null,
    ...overrides,
  };
}

/** A report about player 1's Watchtower at (4, 1), seen in full detail, with no event flagged unless a test flags one. */
function structureReport(overrides: Partial<SightingReport> = {}): SightingReport {
  return report({
    reportType: "StructureSighted",
    stillVisible: false,
    completedInView: false,
    capturedInView: false,
    unitTemplateId: null,
    unitInstanceId: null,
    structureInstanceId: 60,
    structureId: 1,
    completionPercent: 30,
    ...overrides,
  });
}

/** A report about fighting at (4, 1) seen in full detail, with no event flagged unless a test flags one. */
function combatReport(overrides: Partial<SightingReport> = {}): SightingReport {
  return report({
    reportType: "CombatSighted",
    stillVisible: false,
    completedInView: null,
    capturedInView: null,
    unitTemplateId: null,
    unitInstanceId: null,
    ownerPlayerId: null,
    sideJoinedInView: false,
    sideLeftInView: false,
    battleId: 70,
    ...overrides,
  });
}

const CONTEXT: SightingDescriptionContext = {
  unitNameByUnitTemplateId: new Map([[3, "Settler"]]),
  factionNameByPlayerId: new Map([
    [1, "Red Empire"],
    [2, "Blue Empire"],
  ]),
  structureNameByStructureId: new Map([
    [1, "Watchtower"],
    [4, "Outpost"],
  ]),
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

/**
 * A structure report carries every event one watcher saw happen to one structure in a turn, and the sentence says them in the order they can happen.
 */
describe("describeSightingReport for a structure", () => {
  it("says construction started on a structure seen being founded", () => {
    expect(describeSightingReport(structureReport({ appearedInView: true }), CONTEXT)).toBe("Red Empire started construction on a Watchtower at (4, 1).");
  });

  /** Seen founded and finished in the same turn, the start is not news: the report only says it was completed. */
  it("says only that construction was completed when the founding and the finish were both seen", () => {
    expect(describeSightingReport(structureReport({ appearedInView: true, completedInView: true, completionPercent: 100 }), CONTEXT)).toBe(
      "Red Empire completed construction on a Watchtower at (4, 1).",
    );
    expect(describeSightingReport(structureReport({ completedInView: true, completionPercent: 100 }), CONTEXT)).toBe("Red Empire completed construction on a Watchtower at (4, 1).");
  });

  it("names who took a captured structure from whom", () => {
    expect(describeSightingReport(structureReport({ capturedInView: true, previousOwnerPlayerId: 2 }), CONTEXT)).toBe("Red Empire captured Blue Empire's Watchtower at (4, 1).");
  });

  it("says whose structure was razed", () => {
    expect(describeSightingReport(structureReport({ destroyedInView: true, ownerPlayerId: 2 }), CONTEXT)).toBe("Blue Empire's Watchtower at (4, 1) was razed.");
  });

  /** The transient case the feature exists for: founded and razed within one turn is one entry, not two. */
  it("joins the events seen in one turn into one sentence, in order", () => {
    expect(describeSightingReport(structureReport({ appearedInView: true, destroyedInView: true }), CONTEXT)).toBe(
      "Red Empire started construction on a Watchtower at (4, 1), then razed it.",
    );
    expect(describeSightingReport(structureReport({ capturedInView: true, previousOwnerPlayerId: 2, destroyedInView: true }), CONTEXT)).toBe(
      "Red Empire captured Blue Empire's Watchtower at (4, 1), then razed it.",
    );
  });

  /** A glimpse names nobody and no kind, so no sentence can pin the deed on anyone. */
  it("names only what the sighting disclosed", () => {
    const glimpse = { structureId: null, ownerPlayerId: null, structureInstanceId: null, completionPercent: null };

    expect(describeSightingReport(structureReport({ ...glimpse, appearedInView: true }), CONTEXT)).toBe("Construction was started on a structure at (4, 1).");
    expect(describeSightingReport(structureReport({ ...glimpse, capturedInView: true }), CONTEXT)).toBe("A structure at (4, 1) changed hands.");
    expect(describeSightingReport(structureReport({ ...glimpse, destroyedInView: true }), CONTEXT)).toBe("A structure at (4, 1) was razed.");
    expect(describeSightingReport(structureReport({ ...glimpse, appearedInView: true, destroyedInView: true }), CONTEXT)).toBe(
      "Construction was started on a structure at (4, 1), then it was razed.",
    );
  });

  it("falls back to a generic name for a kind the lookup does not know, and uses the right article", () => {
    expect(describeSightingReport(structureReport({ appearedInView: true, structureId: 77 }), CONTEXT)).toBe("Red Empire started construction on a Structure at (4, 1).");
    expect(describeSightingReport(structureReport({ appearedInView: true, structureId: 4 }), CONTEXT)).toBe("Red Empire started construction on an Outpost at (4, 1).");
  });
});

/**
 * Only what a campaign-map watcher could perceive: that fighting broke out, that a side arrived or gave way, that it ended, and whose banners were left behind.
 */
describe("describeSightingReport, for combat", () => {
  it("says fighting broke out when the start was seen, and only that it was seen otherwise", () => {
    expect(describeSightingReport(combatReport({ appearedInView: true }), CONTEXT)).toBe("Fighting broke out at (4, 1).");
    expect(describeSightingReport(combatReport({ sideJoinedInView: true }), CONTEXT)).toBe("Fighting seen at (4, 1), and more forces joined.");
  });

  it("says a force withdrew while the fighting went on, and drops the clause once it has ended", () => {
    expect(describeSightingReport(combatReport({ appearedInView: true, sideLeftInView: true }), CONTEXT)).toBe("Fighting broke out at (4, 1), and a force withdrew.");
    expect(describeSightingReport(combatReport({ appearedInView: true, sideLeftInView: true, destroyedInView: true }), CONTEXT)).toBe(
      "Fighting broke out at (4, 1), and the fighting ended.",
    );
  });

  it("names who was left holding the ground once the ground-holder was disclosed", () => {
    expect(describeSightingReport(combatReport({ appearedInView: true, destroyedInView: true }), CONTEXT)).toBe("Fighting broke out at (4, 1), and the fighting ended.");
    expect(describeSightingReport(combatReport({ appearedInView: true, destroyedInView: true, groundHeldByPlayerId: 1 }), CONTEXT)).toBe(
      "Fighting broke out at (4, 1), and ended with Red Empire holding the ground.",
    );
  });

  it("says the fighting was still going on when it had not ended and was still in view", () => {
    expect(describeSightingReport(combatReport({ appearedInView: true, stillVisible: true }), CONTEXT)).toBe("Fighting broke out at (4, 1), and was still going on.");
  });

  /** The same engagement, read at each fidelity: the sentence grows only as the server discloses more. */
  it("says only as much as the fidelity disclosed", () => {
    const seen = { appearedInView: true, destroyedInView: true };

    expect(describeSightingReport(combatReport({ ...seen, fidelity: "Movement" }), CONTEXT)).toBe("Fighting broke out at (4, 1), and the fighting ended.");
    expect(describeSightingReport(combatReport({ ...seen, fidelity: "Type" }), CONTEXT)).toBe("Fighting broke out at (4, 1), and the fighting ended.");
    expect(describeSightingReport(combatReport({ ...seen, fidelity: "Owner", groundHeldByPlayerId: 1 }), CONTEXT)).toBe(
      "Fighting broke out at (4, 1), and ended with Red Empire holding the ground.",
    );
    expect(describeSightingReport(combatReport({ ...seen, fidelity: "Detail", groundHeldByPlayerId: 1, troopsLost: 120 }), CONTEXT)).toBe(
      "Fighting broke out at (4, 1), and ended with Red Empire holding the ground, with around 120 troops lost.",
    );
  });

  it("names a ground-holder the players list does not know by number rather than dropping them", () => {
    expect(describeSightingReport(combatReport({ destroyedInView: true, groundHeldByPlayerId: 7 }), CONTEXT)).toBe("Fighting seen at (4, 1), and ended with Player 7 holding the ground.");
  });

  it("falls back to the safe sentence when a malformed report flags nothing at all", () => {
    expect(describeSightingReport(combatReport(), CONTEXT)).toBe("Fighting seen at (4, 1).");
  });
});
