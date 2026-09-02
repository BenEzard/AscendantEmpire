import type { ReactNode } from "react";
import { act, renderHook } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { PlayerCampaignMapObserverResultDto } from "@/features/map/api/playerCampaignMapApi";
import { useVisionTool } from "../useVisionTool";

function wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}

/** Builds an observer that can see the given coordinates. Only the coordinates matter to the fog. */
function observer(id: number, seen: [number, number][]): PlayerCampaignMapObserverResultDto {
  return {
    id,
    observerRecordType: 1,
    xCoord: seen[0]?.[0] ?? 0,
    yCoord: seen[0]?.[1] ?? 0,
    seenTiles: seen.map(([x, y]) => ({
      xCoord: x,
      yCoord: y,
      biomeTypeId: 1,
      campaignMapGeographyTypeId: 3,
      structures: [],
      forces: [],
    })),
  };
}

function renderVisionTool() {
  return renderHook(() => useVisionTool({ gameId: 1, playerId: 1, openToast: vi.fn() }), { wrapper });
}

/**
 * The fog overlay is drawn from this set rather than from the tile data, so anything that changes what the player can
 * see has to come through here or the ground stays greyed out however fresh the tiles are.
 */
describe("useVisionTool observed tiles", () => {
  it("starts with nothing observed", () => {
    const { result } = renderVisionTool();

    expect(result.current.observedTileKeys.size).toBe(0);
  });

  it("marks every tile an observer can see", () => {
    const { result } = renderVisionTool();

    act(() => {
      result.current.applyObservers([observer(1, [[2, 3], [2, 4]]), observer(2, [[9, 9]])]);
    });

    expect([...result.current.observedTileKeys].sort()).toEqual(["2,3", "2,4", "9,9"]);
  });

  /**
   * A replacement, not a merge. The observers are the complete current picture, so ground that has dropped out of
   * sight since the last set — because the force watching it moved away — has to go back under fog.
   */
  it("returns fog to ground that is no longer in sight", () => {
    const { result } = renderVisionTool();

    act(() => {
      result.current.applyObservers([observer(1, [[2, 3], [2, 4]])]);
    });

    act(() => {
      result.current.applyObservers([observer(1, [[3, 3], [3, 4]])]);
    });

    expect([...result.current.observedTileKeys].sort()).toEqual(["3,3", "3,4"]);
  });

  it("clears everything when the vision tool is cleared", () => {
    const { result } = renderVisionTool();

    act(() => {
      result.current.applyObservers([observer(1, [[2, 3]])]);
    });

    act(() => {
      result.current.clearVision();
    });

    expect(result.current.observedTileKeys.size).toBe(0);
  });

  /**
   * The set is handed to every memoised Tile through getTileStyle, so a fresh Set on each render would re-render the
   * whole grid whenever anything unrelated on the page changed.
   */
  it("keeps the observed set identity stable while the observers are unchanged", () => {
    const { result, rerender } = renderVisionTool();

    act(() => {
      result.current.applyObservers([observer(1, [[2, 3]])]);
    });

    const firstSet = result.current.observedTileKeys;
    rerender();

    expect(result.current.observedTileKeys).toBe(firstSet);
  });
});
