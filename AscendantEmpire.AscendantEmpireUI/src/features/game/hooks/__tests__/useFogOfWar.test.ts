import { renderHook } from "@testing-library/react";
import type { PlayerCampaignMapObserverResultDto } from "@/features/map/api/playerCampaignMapApi";
import { useFogOfWar } from "../useFogOfWar";

/** Builds a real observer — a force or structure of the player's — that can see the given coordinates. */
function observer(id: number, seen: [number, number][]): PlayerCampaignMapObserverResultDto {
  return {
    id,
    // RecordType.Unit. Anything but None marks an observer that is genuinely watching.
    observerRecordType: 1,
    xCoord: seen[0]?.[0] ?? 0,
    yCoord: seen[0]?.[1] ?? 0,
    seenTiles: seen.map(([x, y]) => ({
      xCoord: x,
      yCoord: y,
      biomeTypeId: 1,
      campaignMapGeographyTypeId: 3,
      isExplored: true,
      structures: [],
      forces: [],
    })),
  };
}

/**
 * Builds the whole-map pseudo-observer, which stands nowhere and watches nothing.
 *
 * The map read uses it to carry back terrain the player explored earlier, in the same shape as a real observer. Its tiles are
 * memory, not sight.
 */
function rememberedTerrain(seen: [number, number][]): PlayerCampaignMapObserverResultDto {
  return { ...observer(0, seen), id: 0, observerRecordType: 0, xCoord: 0, yCoord: 0 };
}

/**
 * The live half of the fog. Everything here is about what the player can see *right now* — what they have explored is the other
 * layer, and it rides on the tiles themselves rather than on this set.
 */
describe("useFogOfWar", () => {
  it("observes nothing when there are no observers", () => {
    const { result } = renderHook(() => useFogOfWar(undefined));

    expect(result.current.observedTileKeys.size).toBe(0);
  });

  it("marks every tile an observer can see", () => {
    const observers = [observer(1, [[2, 3], [2, 4]]), observer(2, [[9, 9]])];

    const { result } = renderHook(() => useFogOfWar(observers));

    expect([...result.current.observedTileKeys].sort()).toEqual(["2,3", "2,4", "9,9"]);
  });

  /**
   * A replacement, not an accumulation. The observers are the complete current picture, so ground that has dropped out of sight
   * since the last set — because the force watching it moved away — is no longer being watched and has to fall back under haze.
   */
  it("stops observing ground that is no longer in sight", () => {
    const { result, rerender } = renderHook(({ observers }) => useFogOfWar(observers), {
      initialProps: { observers: [observer(1, [[2, 3], [2, 4]])] },
    });

    rerender({ observers: [observer(1, [[3, 3], [3, 4]])] });

    expect([...result.current.observedTileKeys].sort()).toEqual(["3,3", "3,4"]);
  });

  /**
   * The pin that keeps the two fog layers apart. The whole-map read hands back every tile the player remembers under a filler
   * observer; counting those as live sight would light the entire explored map and leave the haze layer with nothing to draw.
   */
  it("does not treat remembered terrain from the whole-map read as currently observed", () => {
    const observers = [observer(1, [[2, 3]]), rememberedTerrain([[7, 7], [8, 8]])];

    const { result } = renderHook(() => useFogOfWar(observers));

    expect([...result.current.observedTileKeys]).toEqual(["2,3"]);
  });

  /**
   * The set is handed to every memoised Tile through getTileStyle, so a fresh Set on each render would re-render the whole grid
   * whenever anything unrelated on the page changed.
   */
  it("keeps the observed set identity stable while the observers are unchanged", () => {
    const observers = [observer(1, [[2, 3]])];

    const { result, rerender } = renderHook(() => useFogOfWar(observers));
    const firstSet = result.current.observedTileKeys;

    rerender();

    expect(result.current.observedTileKeys).toBe(firstSet);
  });
});
