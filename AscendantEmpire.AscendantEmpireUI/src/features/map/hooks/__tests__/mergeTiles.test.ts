import { mergeTiles } from "@/features/map/hooks/useCampaignMapTiles";
import type { CampaignMapTileDto, CampaignMapTilesResponse, PlayerCampaignMapObserverResultDto } from "@/features/map/api/playerCampaignMapApi";

function tile(x: number, y: number, isExplored: boolean): CampaignMapTileDto {
  return { xCoord: x, yCoord: y, biomeTypeId: 1, campaignMapGeographyTypeId: 3, isExplored, structures: [], forces: [] };
}

function observer(id: number, seen: [number, number][]): PlayerCampaignMapObserverResultDto {
  return {
    id,
    observerRecordType: 1,
    xCoord: seen[0]?.[0] ?? 0,
    yCoord: seen[0]?.[1] ?? 0,
    seenTiles: seen.map(([x, y]) => tile(x, y, true)),
  };
}

function response(tiles: CampaignMapTileDto[], observers?: PlayerCampaignMapObserverResultDto[]): CampaignMapTilesResponse {
  return observers == null ? { tiles } : { tiles, observers };
}

/**
 * The cache holds both halves of the fog of war, and they have opposite merge rules on purpose: explored terrain accumulates and
 * is never taken back, while the observers are the whole of what is being watched right now and are replaced outright.
 */
describe("mergeTiles", () => {
  it("keeps tiles a later response says nothing about", () => {
    const merged = mergeTiles(response([tile(1, 1, true), tile(2, 2, false)]), response([tile(2, 2, true)]));

    expect(merged.tiles).toHaveLength(2);
    expect(merged.tiles.find((t) => t.xCoord === 1)?.isExplored).toBe(true);
  });

  it("takes the newer explored flag for a tile the response does mention", () => {
    const merged = mergeTiles(response([tile(2, 2, false)]), response([tile(2, 2, true)]));

    expect(merged.tiles.find((t) => t.xCoord === 2)?.isExplored).toBe(true);
  });

  /**
   * A force step reports only what its observers can see, and that set is the complete answer: whatever is missing from it is
   * ground nothing is watching any more. Merging instead of replacing would leave the map lit from where the force used to be.
   */
  it("replaces the observers when the response carries them", () => {
    const merged = mergeTiles(
      response([tile(1, 1, true)], [observer(1, [[1, 1]])]),
      response([tile(2, 2, true)], [observer(1, [[2, 2]])]),
    );

    expect(merged.observers?.flatMap((o) => o.seenTiles.map((t) => `${t.xCoord},${t.yCoord}`))).toEqual(["2,2"]);
  });

  /** Not every write to the cache describes what the player can see, and one that doesn't must not blank the live sight layer. */
  it("leaves the observers alone when the response carries none", () => {
    const merged = mergeTiles(response([tile(1, 1, true)], [observer(1, [[1, 1]])]), response([tile(2, 2, true)]));

    expect(merged.observers?.flatMap((o) => o.seenTiles.map((t) => `${t.xCoord},${t.yCoord}`))).toEqual(["1,1"]);
  });

  it("takes the whole response when there is nothing cached yet", () => {
    const first = response([tile(1, 1, true)], [observer(1, [[1, 1]])]);

    expect(mergeTiles(undefined, first)).toBe(first);
  });
});
