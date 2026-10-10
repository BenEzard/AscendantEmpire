import { mergeTilesByCoord, type CampaignMapTileDto, type ObservedForceDto } from "../playerCampaignMapApi";

function force(id: number): ObservedForceDto {
  return {
    id,
    recordType: 1,
    ownerPlayerId: 1,
    unitTemplateId: 1,
    healthyTroops: 50,
    injuredTroops: 0,
    initialStrength: 50,
    hasCampaignMovementRemaining: true,
    campaignMovementRemaining: 2,
    campaignMovementPoints: 2,
    // Nothing spends battle movement yet, so it's irrelevant to merging; null is its masked value.
    battleMovementRemaining: null,
  };
}

function tile(x: number, y: number, forces: ObservedForceDto[] = []): CampaignMapTileDto {
  // Biome and Geography arrive as their enum member names, not numbers — see BiomeType in @/types/map/Biome.
  return { xCoord: x, yCoord: y, biomeTypeId: "Coastal", campaignMapGeographyTypeId: "Flat", isExplored: true, structures: [], forces };
}

describe("mergeTilesByCoord", () => {
  it("collapses the same tile seen by several observers into one entry", () => {
    const merged = mergeTilesByCoord([tile(1, 1), tile(2, 2), tile(1, 1)]);

    expect(merged).toHaveLength(2);
  });

  it("lets an empty force list overwrite a populated one, so a force that stepped away stops being drawn", () => {
    // This is the case that mattered: the tile a force left is re-reported with no forces on it, and treating that
    // as "nothing to say" left the force painted on a tile it was no longer standing on.
    const merged = mergeTilesByCoord([tile(1, 1, [force(7)]), tile(1, 1, [])]);

    expect(merged[0].forces).toEqual([]);
  });

  it("takes the newer force list when there is one", () => {
    const merged = mergeTilesByCoord([tile(1, 1, [force(7)]), tile(1, 1, [force(9)])]);

    expect(merged[0].forces.map((f) => f.id)).toEqual([9]);
  });
});
