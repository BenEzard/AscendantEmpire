import type { KnownStructure } from "../api/intelligenceApi";
import { groupKnownStructuresByUnobservedTile } from "../knownStructureMarkers";

function knownStructure(x: number, y: number, lastSeenPlayerTurnId: number, lastSeenTurnSequence = 1): KnownStructure {
  return {
    status: "Standing",
    fidelity: "Detail",
    location: { x, y },
    lastSeenPlayerTurnId,
    lastSeenRoundNumber: 1,
    lastSeenTurnSequence,
    stillVisible: false,
    structureId: 1,
    ownerPlayerId: 1,
    structureInstanceId: 60,
    completionPercent: 100,
  };
}

describe("groupKnownStructuresByUnobservedTile", () => {
  /**
   * Under live sight the map already shows what really stands on a tile, so a memory there would at best repeat it and at worst draw a structure the player can
   * see has been razed.
   */
  it("leaves out every tile currently in sight", () => {
    const grouped = groupKnownStructuresByUnobservedTile([knownStructure(2, 3, 10), knownStructure(5, 5, 10)], new Set(["2,3"]));

    expect([...grouped.keys()]).toEqual(["5,5"]);
  });

  it("puts the freshest sighting on a tile first, since that is the one the marker shows", () => {
    const older = knownStructure(5, 5, 10, 8);
    const newerTurn = knownStructure(5, 5, 12, 1);
    const laterInSameTurn = knownStructure(5, 5, 10, 9);

    const grouped = groupKnownStructuresByUnobservedTile([older, newerTurn, laterInSameTurn], new Set());

    expect(grouped.get("5,5")).toEqual([newerTurn, laterInSameTurn, older]);
  });
});
