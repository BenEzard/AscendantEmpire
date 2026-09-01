import type { ReactNode } from "react";
import { act, renderHook } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ObservedForceTile, TileCell } from "@/features/map/components/TileTypes";
import { useForceSelection } from "../useForceSelection";

// The hook queries the backend for the selected force's context actions. Those are the toolbar's business, not
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
    expect(result.current.selectedForce?.id).toBe(1);
  });

  it("selects nothing on a tile holding only another player's forces", () => {
    const { result } = renderForceSelection();

    act(() => {
      result.current.selectForceFromTile(tile([force({ id: 1, ownerPlayerId: OTHER_PLAYER_ID })]));
    });

    expect(result.current.selectedForce).toBeNull();
    expect(result.current.multiForceTile).toBeNull();
  });

  it("selects the strongest owned force by default on a multi-force tile", () => {
    const { result } = renderForceSelection();

    act(() => {
      result.current.selectForceFromTile(
        tile([force({ id: 1, healthyTroops: 10 }), force({ id: 2, healthyTroops: 90 })]),
      );
    });

    expect(result.current.selectedForce?.id).toBe(2);
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

    expect(result.current.selectedForce?.id).toBe(1);
    // Choosing also decides which force is drawn on that tile, so the map agrees with the bar.
    expect(result.current.getVisibleForceId(tile([weakerForce, force({ id: 2, healthyTroops: 90 })]))).toBe(1);
  });

  it("clears the checked forces when the selection moves to another tile", () => {
    const { result } = renderForceSelection();

    act(() => {
      result.current.selectForceFromTile(tile([force({ id: 1 }), force({ id: 2 })]));
    });
    act(() => {
      result.current.toggleForceChecked(1, true);
    });

    expect(result.current.checkedForceIds.has(1)).toBe(true);

    act(() => {
      result.current.selectForceFromTile({ ...tile([force({ id: 3 }), force({ id: 4 })]), x: 9, y: 9 });
    });

    expect(result.current.checkedForceIds.size).toBe(0);
  });
});
