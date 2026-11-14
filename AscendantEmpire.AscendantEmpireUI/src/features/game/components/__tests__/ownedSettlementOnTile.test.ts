import type { ObservedStructureTile, TileCell } from "@/features/map/components/TileTypes";
import type { TileOwnership } from "@/types/map/Ownership";
import { ownedSettlementOnTile } from "../ownedSettlementOnTile";

const PLAYER_ID = 1;
const RIVAL_ID = 2;

function structure(overrides: Partial<ObservedStructureTile> = {}): ObservedStructureTile {
  return { id: 7, structureId: 1, structureCode: "VILLAGE", settlementName: "Ravenhold", positionOnTile: 1, completionPercent: 100, ...overrides };
}

function ownedBy(playerId: number | null, overrides: Partial<TileOwnership> = {}): TileOwnership {
  return {
    status: playerId == null ? "Unclaimed" : "Owned",
    ownedByPlayerId: playerId,
    occupiedByPlayerId: null,
    lastOwnedByPlayerId: null,
    effectiveHolderPlayerId: playerId,
    claimedByPlayerIds: [],
    isRebelling: false,
    ...overrides,
  };
}

function tile(structures?: ObservedStructureTile[], ownership?: TileOwnership): TileCell {
  // Biome and Geography arrive as their enum member names, not numbers — see BiomeType in @/types/map/Biome.
  return { x: 3, y: 4, biomeTypeId: "Grassland", geographyTypeId: "Flat", isExplored: true, structures, ownership };
}

describe("ownedSettlementOnTile", () => {
  it("finds the settlement standing on the player's own ground", () => {
    expect(ownedSettlementOnTile(tile([structure()], ownedBy(PLAYER_ID)), PLAYER_ID)?.settlementName).toBe("Ravenhold");
  });

  /** The manager is for the player's own settlements. A rival's city is drawn on the map and named on it, but not opened. */
  it("finds nothing in a rival's settlement", () => {
    expect(ownedSettlementOnTile(tile([structure()], ownedBy(RIVAL_ID)), PLAYER_ID)).toBeNull();
  });

  it("finds nothing on ground nobody owns", () => {
    expect(ownedSettlementOnTile(tile([structure()], ownedBy(null)), PLAYER_ID)).toBeNull();
  });

  /**
   * Ownership is current information, so the map read reports it only where an observer is watching — see TileOwnership. Ground
   * the player merely remembers therefore says nothing about who holds it, and "we have not been told" is not "it is yours".
   */
  it("finds nothing where the map read reported no ownership at all", () => {
    expect(ownedSettlementOnTile(tile([structure()]), PLAYER_ID)).toBeNull();
  });

  /** An occupier takes no ownership, so a settlement under enemy occupation is still the owner's to manage. */
  it("finds the settlement on the player's ground even while a rival occupies it", () => {
    const occupied = ownedBy(PLAYER_ID, { status: "Occupied", occupiedByPlayerId: RIVAL_ID, effectiveHolderPlayerId: RIVAL_ID });

    expect(ownedSettlementOnTile(tile([structure()], occupied), PLAYER_ID)?.settlementName).toBe("Ravenhold");
  });

  /** A structure is only somebody's settlement when it carries a name — a watchtower or a totem pole on the same tile is not one. */
  it("finds nothing on a tile whose structures are no-one's settlement", () => {
    expect(ownedSettlementOnTile(tile([structure({ settlementName: null })], ownedBy(PLAYER_ID)), PLAYER_ID)).toBeNull();
  });

  it("finds the settlement among structures that are not settlements", () => {
    const settlement = structure({ id: 9, settlementName: "Thornmere" });

    expect(ownedSettlementOnTile(tile([structure({ settlementName: null }), settlement], ownedBy(PLAYER_ID)), PLAYER_ID)).toBe(settlement);
  });

  it("finds nothing on bare ground", () => {
    expect(ownedSettlementOnTile(tile(undefined, ownedBy(PLAYER_ID)), PLAYER_ID)).toBeNull();
    expect(ownedSettlementOnTile(tile([], ownedBy(PLAYER_ID)), PLAYER_ID)).toBeNull();
  });

  /** The map is up before the game token has named the player, and nothing is anyone's until it has. */
  it("finds nothing while there is no player to own anything", () => {
    expect(ownedSettlementOnTile(tile([structure()], ownedBy(PLAYER_ID)), null)).toBeNull();
  });
});
