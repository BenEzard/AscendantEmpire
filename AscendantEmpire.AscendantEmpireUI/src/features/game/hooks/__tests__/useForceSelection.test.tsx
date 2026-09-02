import type { ReactNode } from "react";
import { act, renderHook } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ObservedForceTile, TileCell } from "@/features/map/components/TileTypes";
import { useForceSelection } from "../useForceSelection";

// The hook queries the backend for the selection's context actions. Those are the toolbar's business, not
// this file's, so the request is stubbed away rather than served.
vi.mock("../../api/forceContextActionsApi", () => ({
  getForceContextActions: vi.fn(async () => []),
}));

const THIS_PLAYER_ID = 1;
const OTHER_PLAYER_ID = 2;

function wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}

/** Builds a force, owned by the acting player unless told otherwise. */
function force(overrides: Partial<ObservedForceTile> = {}): ObservedForceTile {
  return {
    id: 1,
    recordType: 1,
    ownerPlayerId: THIS_PLAYER_ID,
    unitTemplateId: 1,
    healthyTroops: 50,
    injuredTroops: 0,
    initialStrength: 50,
    hasMovementRemaining: true,
    ...overrides,
  };
}

function tile(forces: ObservedForceTile[]): TileCell {
  return { x: 3, y: 4, biomeTypeId: 1, geographyTypeId: 1, forces };
}

function renderForceSelection() {
  return renderHook(() => useForceSelection({ gameId: 1, playerId: THIS_PLAYER_ID, openToast: vi.fn() }), { wrapper });
}

/** The selection as ids, in selection order — what nearly every assertion below is about. */
function selectedIds(result: { current: ReturnType<typeof useForceSelection> }): number[] {
  return result.current.selectedForces.map((f) => f.id);
}

describe("useForceSelection", () => {
  /**
   * The bar shows whatever it's given, so ownership has to be filtered before it gets there. An enemy or ally
   * sharing the tile is still drawn on the map — it just isn't something this player can select.
   */
  it("offers only the acting player's own forces when a tile holds several players' forces", () => {
    const { result } = renderForceSelection();

    act(() => {
      result.current.selectForceFromTile(
        tile([
          force({ id: 1 }),
          force({ id: 2, ownerPlayerId: OTHER_PLAYER_ID }),
          force({ id: 3 }),
          force({ id: 4, ownerPlayerId: OTHER_PLAYER_ID }),
        ]),
      );
    });

    expect(result.current.multiForceTile?.forces.map((f) => f.id)).toEqual([1, 3]);
  });

  /**
   * Two forces on the tile but only one of them the player's is a single-force tile as far as selection goes —
   * there is nothing to choose between, so the bar stays away.
   */
  it("offers no bar when the player owns only one of the forces on the tile", () => {
    const { result } = renderForceSelection();

    act(() => {
      result.current.selectForceFromTile(tile([force({ id: 1 }), force({ id: 2, ownerPlayerId: OTHER_PLAYER_ID })]));
    });

    expect(result.current.multiForceTile).toBeNull();
    expect(selectedIds(result)).toEqual([1]);
  });

  it("selects nothing on a tile holding only another player's forces", () => {
    const { result } = renderForceSelection();

    act(() => {
      result.current.selectForceFromTile(tile([force({ id: 1, ownerPlayerId: OTHER_PLAYER_ID })]));
    });

    expect(result.current.selectedForces).toEqual([]);
    expect(result.current.multiForceTile).toBeNull();
  });

  it("selects the strongest owned force by default on a multi-force tile", () => {
    const { result } = renderForceSelection();

    act(() => {
      result.current.selectForceFromTile(
        tile([force({ id: 1, healthyTroops: 10 }), force({ id: 2, healthyTroops: 90 })]),
      );
    });

    expect(selectedIds(result)).toEqual([2]);
  });

  it("changes the selection when a force is chosen from the bar", () => {
    const { result } = renderForceSelection();
    const weakerForce = force({ id: 1, healthyTroops: 10 });

    act(() => {
      result.current.selectForceFromTile(tile([weakerForce, force({ id: 2, healthyTroops: 90 })]));
    });
    act(() => {
      result.current.chooseForce(weakerForce);
    });

    expect(selectedIds(result)).toEqual([1]);
    // Choosing also decides which force is drawn on that tile, so the map agrees with the bar.
    expect(result.current.getVisibleForceId(tile([weakerForce, force({ id: 2, healthyTroops: 90 })]))).toBe(1);
  });

  /**
   * The agreed multi-select behaviour, walked gesture by gesture. It is kept as one sequence rather than split
   * into a test per row because the rules are about *transitions* — "a plain click clears first" and "ctrl+click
   * removes what is already selected" only mean anything against the state the previous gesture left behind.
   */
  it("appends on ctrl+click, removes an already-selected force, and replaces the lot on a plain click", () => {
    const { result } = renderForceSelection();

    const forceA = force({ id: 1 });
    const forceB = force({ id: 2 });
    const forceC = force({ id: 3 });

    act(() => {
      result.current.selectForceFromTile(tile([forceA, forceB, forceC]));
    });

    // Left click A: A is selected.
    act(() => result.current.chooseForce(forceA, false));
    expect(selectedIds(result)).toEqual([1]);

    // Left click B: B is selected (A dropped).
    act(() => result.current.chooseForce(forceB, false));
    expect(selectedIds(result)).toEqual([2]);

    // Ctrl+click B: B was selected, so it comes out — leaving nothing.
    act(() => result.current.chooseForce(forceB, true));
    expect(selectedIds(result)).toEqual([]);

    // Left click A: A is selected.
    act(() => result.current.chooseForce(forceA, false));
    expect(selectedIds(result)).toEqual([1]);

    // Ctrl+click B: A and B.
    act(() => result.current.chooseForce(forceB, true));
    expect(selectedIds(result)).toEqual([1, 2]);

    // Ctrl+click C: A, B and C.
    act(() => result.current.chooseForce(forceC, true));
    expect(selectedIds(result)).toEqual([1, 2, 3]);

    // Ctrl+click B: B was selected, so it comes out of the middle — A and C.
    act(() => result.current.chooseForce(forceB, true));
    expect(selectedIds(result)).toEqual([1, 3]);

    // Left click B: a plain click always clears first, so only B survives.
    act(() => result.current.chooseForce(forceB, false));
    expect(selectedIds(result)).toEqual([2]);
  });

  /**
   * Deselecting the last force is allowed, and is why the dock stays mounted on a tile with a bar: if it
   * unmounted here, the bar would vanish out from under the cursor that was clicking in it.
   */
  it("empties the selection when the only selected force is ctrl+clicked", () => {
    const { result } = renderForceSelection();
    const onlyForce = force({ id: 1 });

    act(() => {
      result.current.selectForceFromTile(tile([onlyForce, force({ id: 2 })]));
    });
    act(() => result.current.chooseForce(onlyForce, false));
    act(() => result.current.chooseForce(onlyForce, true));

    expect(result.current.selectedForces).toEqual([]);
    expect(result.current.selectedForceIds.size).toBe(0);
    // The bar still has a tile to render, so the dock has a reason to stay on screen.
    expect(result.current.multiForceTile).not.toBeNull();
  });

  /**
   * The tile draws the force the player last *selected*. Pinning on a removing ctrl+click would leave the map
   * showing the one force the player has just taken out of the selection.
   */
  it("leaves the tile's drawn force alone when a ctrl+click removes a force", () => {
    const { result } = renderForceSelection();

    const forceA = force({ id: 1, healthyTroops: 10 });
    const forceB = force({ id: 2, healthyTroops: 90 });
    const tileCell = tile([forceA, forceB]);

    act(() => {
      result.current.selectForceFromTile(tileCell);
    });
    act(() => result.current.chooseForce(forceA, false));

    expect(result.current.getVisibleForceId(tileCell)).toBe(1);

    // Add B (which pins B), then take B straight back out again.
    act(() => result.current.chooseForce(forceB, true));
    expect(result.current.getVisibleForceId(tileCell)).toBe(2);

    act(() => result.current.chooseForce(forceB, true));

    expect(selectedIds(result)).toEqual([1]);
    expect(result.current.getVisibleForceId(tileCell)).toBe(2);
  });

  /** Clicking the map is how a fresh selection starts, so it replaces a multi-force selection rather than adding to it. */
  it("resets to a single force when a tile is clicked with several forces selected", () => {
    const { result } = renderForceSelection();

    const forceA = force({ id: 1 });
    const forceB = force({ id: 2 });

    act(() => {
      result.current.selectForceFromTile(tile([forceA, forceB]));
    });
    act(() => result.current.chooseForce(forceA, false));
    act(() => result.current.chooseForce(forceB, true));

    expect(selectedIds(result)).toEqual([1, 2]);

    act(() => {
      result.current.selectForceFromTile({ ...tile([force({ id: 7 })]), x: 9, y: 9 });
    });

    expect(selectedIds(result)).toEqual([7]);
  });

  /**
   * The id set is handed to every memoised Tile. A fresh Set on each render would defeat that memo and re-render
   * the whole grid whenever anything else in the page changed.
   */
  it("keeps the selected id set identity stable while the selection is unchanged", () => {
    const { result, rerender } = renderForceSelection();

    act(() => {
      result.current.selectForceFromTile(tile([force({ id: 1 }), force({ id: 2 })]));
    });

    const firstSet = result.current.selectedForceIds;
    rerender();

    expect(result.current.selectedForceIds).toBe(firstSet);
  });
});
