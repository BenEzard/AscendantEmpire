import type { SightingReport } from "@/features/intelligence/api/intelligenceApi";
import { buildSightingReportGroups } from "../sightingReportNotifications";

function report(reportId: number, roundNumber: number, path: { x: number; y: number }[]): SightingReport {
  return {
    reportId,
    reportType: "ForceSighted",
    raisedDuringPlayerTurnId: 10 + reportId,
    roundNumber,
    fidelity: "Movement",
    path,
    exitDirection: null,
    stillVisible: true,
    appearedInView: false,
    destroyedInView: false,
    firstTurnSequence: 1,
    lastTurnSequence: 1,
    firstWitnessId: 50,
    firstWitnessRecordType: "Structure",
    firstWitnessOwnerPlayerId: 2,
    unitTemplateId: null,
    ownerPlayerId: null,
    unitInstanceId: null,
    healthyTroops: null,
  };
}

describe("buildSightingReportGroups", () => {
  /** A quiet round should add nothing to the panel, not an empty "Sightings (0)" heading. */
  it("adds no group for an empty feed", () => {
    expect(buildSightingReportGroups([], {})).toEqual([]);
  });

  /**
   * The API lists the feed oldest first, but a feed reads newest first. The ids have to be stable across refetches, or a dismissed report would come straight back.
   */
  it("lists reports newest first under one Sightings group, with stable ids", () => {
    const [group] = buildSightingReportGroups([report(1, 1, [{ x: 1, y: 1 }]), report(2, 2, [{ x: 2, y: 2 }])], {});

    expect(group.type).toBe("sighting-reports");
    expect(group.label).toBe("Sightings");
    expect(group.items.map((item) => item.id)).toEqual(["sighting:2", "sighting:1"]);
    expect(group.items.map((item) => item.detail)).toEqual(["Round 2", "Round 1"]);
  });

  /** Clicking a report pans the map, and the useful place to look is where the force was last seen, not where it was first picked up. */
  it("points each item at the last tile the force was seen on", () => {
    const [group] = buildSightingReportGroups([report(1, 1, [{ x: 1, y: 1 }, { x: 2, y: 1 }, { x: 3, y: 2 }])], {});

    expect(group.items[0].location).toEqual({ x: 3, y: 2 });
    expect(group.items[0].label).toBe("Something seen passing through (1, 1), (2, 1), (3, 2).");
  });
});
