import type { ReactNode } from "react";
import { act, renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ObservedForceTile, TileCell } from "@/features/map/components/TileTypes";
import { getForceContextActions } from "../../api/forceContextActionsApi";
import type { StepForceResponse } from "../../api/forceMovementApi";
import { useForceSelection } from "../useForceSelection";

// The hook queries the backend for the selection's context actions. Those are the toolbar's business, not
// this file's, so the request is stubbed away rather than served.
vi.mock("../../api/forceContextActionsApi", () => ({
  getForceContextActions: vi.fn(async () => []),
}));

// An unnamed tier founds without a dialog, so the hook itself posts the order. isNamedTier is kept real: which tiers carry a
// name is the rule under test here, not something the test should get to decide.
const settleForce = vi.hoisted(() => vi.fn());

vi.mock("../../api/settleForceApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../../api/settleForceApi")>()),
  settleForce,
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
    hasCampaignMovementRemaining: true,
    campaignMovementRemaining: 2,
    campaignMovementPoints: 2,
    // Nothing spends battle movement yet, so the selection logic never reads it; null is its masked value.
    battleMovementRemaining: null,
    ...overrides,
  };
}

function tile(forces: ObservedForceTile[]): TileCell {
  // Biome and Geography arrive as their enum member names, not numbers — see BiomeType in @/types/map/Biome.
  return { x: 3, y: 4, biomeTypeId: "Coastal", geographyTypeId: "Flat", isExplored: true, forces };
}

/**
 * Builds what the step endpoint answers with: where the force ended up, and the player's refreshed view of the map.
 *
 * `forcesOnDestination` is everything standing on the destination tile *after* the move — the mover included — which is what
 * the hook reads to re-point the selection bar.
 */
function stepResult({
  unitInstanceId = 1,
  x,
  y,
  campaignMovementRemaining = 1,
  forcesOnDestination,
}: {
  unitInstanceId?: number;
  x: number;
  y: number;
  campaignMovementRemaining?: number;
  forcesOnDestination?: ObservedForceTile[];
}): StepForceResponse {
  return {
    unitInstanceId,
    xCoord: x,
    yCoord: y,
    campaignMovementRemaining,
    campaignMovementPoints: 2,
    stepCost: 1,
    riverMovementCost: 0,
    startedBattle: false,
    riverCrossed: "None",
    riverAlong: "None",
    tiles:
      forcesOnDestination == null
        ? []
        : [
            {
              xCoord: x,
              yCoord: y,
              biomeTypeId: "Coastal",
              campaignMapGeographyTypeId: "Flat",
              isExplored: true,
              structures: [],
              forces: forcesOnDestination,
            },
          ],
    observers: [],
  };
}

function renderForceSelection(openToast: (message: string, variant?: never) => void = vi.fn()) {
  return renderHook(() => useForceSelection({ gameId: 1, playerId: THIS_PLAYER_ID, openToast }), { wrapper });
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

    expect(result.current.forceSelectionTile?.forces.map((f) => f.id)).toEqual([1, 3]);
  });

  /**
   * A lone force still gets the bar — it is where the force's strength and remaining movement are shown — and an enemy
   * sharing the tile doesn't count towards it.
   */
  it("offers the bar for a single owned force, leaving out an enemy sharing the tile", () => {
    const { result } = renderForceSelection();

    act(() => {
      result.current.selectForceFromTile(tile([force({ id: 1 }), force({ id: 2, ownerPlayerId: OTHER_PLAYER_ID })]));
    });

    expect(result.current.forceSelectionTile?.forces.map((f) => f.id)).toEqual([1]);
    expect(selectedIds(result)).toEqual([1]);
  });

  it("selects nothing on a tile holding only another player's forces", () => {
    const { result } = renderForceSelection();

    act(() => {
      result.current.selectForceFromTile(tile([force({ id: 1, ownerPlayerId: OTHER_PLAYER_ID })]));
    });

    expect(result.current.selectedForces).toEqual([]);
    expect(result.current.forceSelectionTile).toBeNull();
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
    expect(result.current.forceSelectionTile).not.toBeNull();
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

  /**
   * Holding a tile orders the selected force onto it. The hook decides what the gesture *means* — the caller owns
   * the request and the messaging — so these pin the verdict rather than any API call.
   */
  describe("getStepToTile", () => {
    /** Puts a force on a tile at the given coordinate and selects it, returning the rendered hook. */
    function selectForceAt(x: number, y: number, forceId = 1) {
      const view = renderForceSelection();

      act(() => {
        view.result.current.selectForceFromTile({ ...tile([force({ id: forceId })]), x, y });
      });

      return view;
    }

    it("steps to a neighbouring tile, working out the direction from where the force stands", () => {
      const { result } = selectForceAt(5, 4);

      expect(result.current.getStepToTile({ ...tile([]), x: 6, y: 3 })).toEqual({
        kind: "step",
        unitInstanceId: 1,
        direction: "NorthEast",
      });
    });

    it("refuses a tile further than one step away", () => {
      const { result } = selectForceAt(5, 4);

      expect(result.current.getStepToTile({ ...tile([]), x: 9, y: 4 }).kind).toBe("too-far");
    });

    it("treats holding the tile the force already stands on as no move at all", () => {
      const { result } = selectForceAt(5, 4);

      expect(result.current.getStepToTile({ ...tile([]), x: 5, y: 4 }).kind).toBe("already-here");
    });

    it("does nothing when nothing is selected", () => {
      const { result } = renderForceSelection();

      expect(result.current.getStepToTile({ ...tile([]), x: 5, y: 4 }).kind).toBe("nothing-selected");
    });

    it("refuses to move an arbitrary one of several selected forces", () => {
      const { result } = selectForceAt(5, 4);

      act(() => {
        result.current.chooseForce(force({ id: 2 }), true);
      });

      expect(result.current.getStepToTile({ ...tile([]), x: 6, y: 4 }).kind).toBe("multiple-selected");
    });

    /**
     * The recorded origin has to follow the force, or the second hold of a two-tile walk would be judged against
     * where it set off from and come out one tile short.
     */
    it("judges the next hold against where the force moved to, not where it started", () => {
      const { result } = selectForceAt(5, 4);

      act(() => {
        result.current.onForceMoved(stepResult({ x: 6, y: 4, forcesOnDestination: [force({ id: 1 })] }));
      });

      expect(result.current.getStepToTile({ ...tile([]), x: 7, y: 4 })).toEqual({
        kind: "step",
        unitInstanceId: 1,
        direction: "East",
      });

      // And the tile it set off from is now two steps away, so it is out of reach rather than still adjacent.
      expect(result.current.getStepToTile({ ...tile([]), x: 4, y: 4 }).kind).toBe("too-far");
    });

    /**
     * The bar's forces are all on one tile, so picking a different one there has to find an origin already
     * recorded — selecting from the map stores every owned force on the tile, not just the one it selects.
     */
    it("can step a force chosen from the selection bar rather than the map", () => {
      const { result } = renderForceSelection();

      act(() => {
        result.current.selectForceFromTile({ ...tile([force({ id: 1 }), force({ id: 2 })]), x: 5, y: 4 });
      });

      act(() => {
        result.current.chooseForce(force({ id: 2 }));
      });

      expect(result.current.getStepToTile({ ...tile([]), x: 5, y: 5 })).toEqual({
        kind: "step",
        unitInstanceId: 2,
        direction: "South",
      });
    });
  });

  /**
   * The bar describes one tile's forces. When the selected force leaves that tile the bar has to follow it, or it goes on
   * offering the tile it set off from — including the force that is no longer standing there.
   */
  describe("the selection bar after a move", () => {
    /** Selects one of three forces sharing a tile, which is the case the bar exists for. */
    function selectFromCrowdedTile() {
      const view = renderForceSelection();

      act(() => {
        view.result.current.selectForceFromTile({
          ...tile([force({ id: 1 }), force({ id: 2 }), force({ id: 3 })]),
          x: 5,
          y: 4,
        });
      });

      return view;
    }

    it("re-points the bar at the destination tile when the force arrives among others", () => {
      const { result } = selectFromCrowdedTile();

      expect(result.current.forceSelectionTile).toMatchObject({ x: 5, y: 4 });

      act(() => {
        result.current.onForceMoved(
          stepResult({ x: 6, y: 4, forcesOnDestination: [force({ id: 1 }), force({ id: 8 })] }),
        );
      });

      expect(result.current.forceSelectionTile).toMatchObject({ x: 6, y: 4 });
      expect(result.current.forceSelectionTile?.forces.map((f) => f.id)).toEqual([1, 8]);
    });

    it("keeps the bar, showing only the mover, when the force moves somewhere it stands alone", () => {
      const { result } = selectFromCrowdedTile();

      act(() => {
        result.current.onForceMoved(stepResult({ x: 6, y: 4, forcesOnDestination: [force({ id: 1 })] }));
      });

      // A lone force still gets the bar, which is the same rule that governs selecting a tile by hand.
      expect(result.current.forceSelectionTile).toMatchObject({ x: 6, y: 4 });
      expect(result.current.forceSelectionTile?.forces.map((f) => f.id)).toEqual([1]);
    });

    it("leaves the moved force selected so it can be moved again straight away", () => {
      const { result } = selectFromCrowdedTile();

      act(() => {
        result.current.onForceMoved(
          stepResult({ x: 6, y: 4, forcesOnDestination: [force({ id: 1 }), force({ id: 8 })] }),
        );
      });

      expect(selectedIds(result)).toEqual([1]);
    });

    it("offers only the player's own forces on the destination, never an enemy sharing it", () => {
      const { result } = selectFromCrowdedTile();

      act(() => {
        result.current.onForceMoved(
          stepResult({
            x: 6,
            y: 4,
            forcesOnDestination: [force({ id: 1 }), force({ id: 8 }), force({ id: 9, ownerPlayerId: OTHER_PLAYER_ID })],
          }),
        );
      });

      expect(result.current.forceSelectionTile?.forces.map((f) => f.id)).toEqual([1, 8]);
    });

    it("takes the moved force's fresh state from the response rather than patching the snapshot it held", () => {
      const { result } = selectFromCrowdedTile();

      act(() => {
        result.current.onForceMoved(
          stepResult({
            x: 6,
            y: 4,
            forcesOnDestination: [force({ id: 1, campaignMovementRemaining: 0.5, healthyTroops: 44 })],
          }),
        );
      });

      const moved = result.current.selectedForces[0];
      expect(moved.campaignMovementRemaining).toBe(0.5);
      // Troop count too, not just movement: every field should be current, not only the one we knew had changed.
      expect(moved.healthyTroops).toBe(44);
    });

    it("still records the movement spent when refreshing the map view returned nothing", () => {
      const { result } = selectFromCrowdedTile();

      // The step committed but the vision refresh failed, so the response carries no tiles at all.
      act(() => {
        result.current.onForceMoved(stepResult({ x: 6, y: 4, campaignMovementRemaining: 0.5 }));
      });

      expect(result.current.selectedForces[0].campaignMovementRemaining).toBe(0.5);
      // The bar cannot describe the destination without data for it, and a bar for the tile the force left would be
      // worse than none at all.
      expect(result.current.forceSelectionTile).toBeNull();
    });

    it("lets a force picked up on the destination tile be moved without reselecting it from the map", () => {
      const { result } = selectFromCrowdedTile();

      act(() => {
        result.current.onForceMoved(
          stepResult({ x: 6, y: 4, forcesOnDestination: [force({ id: 1 }), force({ id: 8 })] }),
        );
      });

      act(() => {
        result.current.chooseForce(force({ id: 8 }));
      });

      expect(result.current.getStepToTile({ ...tile([]), x: 7, y: 4 })).toEqual({
        kind: "step",
        unitInstanceId: 8,
        direction: "East",
      });
    });
  });

  /**
   * The Battle button is backend-driven like every other action; what the hook owns is turning a press into the dialog opening
   * for the right force, and closing it again.
   */
  describe("the Battle action", () => {
    it("opens the battle dialog for the selected force, and closes it again", async () => {
      // Only this test's selection is in battle, so the backend offers the Battle action for it.
      vi.mocked(getForceContextActions).mockResolvedValueOnce([
        { actionType: "Battle", label: "Battle", displayOrder: 1300, isEnabled: true, disabledReasons: [] },
      ]);

      const { result } = renderForceSelection();

      act(() => {
        result.current.selectForceFromTile(tile([force({ id: 7 })]));
      });

      // The actions arrive asynchronously from the (mocked) query.
      await waitFor(() => expect(result.current.contextActionToolbarActions.map((action) => action.key)).toEqual(["Battle"]));

      expect(result.current.battleDialogForceId).toBeNull();

      act(() => result.current.contextActionToolbarActions[0].onClick());

      expect(result.current.battleDialogForceId).toBe(7);

      act(() => result.current.closeBattleDialog());

      expect(result.current.battleDialogForceId).toBeNull();
    });
  });

  /**
   * The two settlement actions part company over whether the player names what they are founding. A village is named, so its
   * button can only open a dialog and ask; a camp is not, so its button has nothing to ask for and founds outright.
   */
  describe("the settlement actions", () => {
    beforeEach(() => {
      settleForce.mockReset();
    });

    /** Selects one force whose only available action is the one given, and returns the hook. */
    async function withSettlementAction(actionType: "CreateVillage" | "EstablishCamp", label: string, openToast = vi.fn()) {
      vi.mocked(getForceContextActions).mockResolvedValueOnce([
        { actionType, label, displayOrder: 1100, isEnabled: true, disabledReasons: [] },
      ]);

      const { result } = renderForceSelection(openToast);

      act(() => {
        result.current.selectForceFromTile(tile([force({ id: 7 })]));
      });

      await waitFor(() =>
        expect(result.current.contextActionToolbarActions.map((action) => action.key)).toEqual([actionType]),
      );

      return result;
    }

    it("opens the settlement dialog at the Village tier, and closes it again", async () => {
      const result = await withSettlementAction("CreateVillage", "Create Village");

      expect(result.current.settlementDialogTier).toBeNull();

      act(() => result.current.contextActionToolbarActions[0].onClick());

      expect(result.current.settlementDialogTier).toBe("Village");

      act(() => result.current.closeSettlementDialog());

      expect(result.current.settlementDialogTier).toBeNull();
    });

    /** A camp is a place the settlers stopped rather than one they founded, so there is no name to collect and no dialog. */
    it("founds a camp outright, with no name and no dialog", async () => {
      settleForce.mockResolvedValue({
        settlementId: 7,
        structureInstanceId: 91,
        name: null,
        tier: "Camp",
        population: 100,
        xCoord: 4,
        yCoord: 1,
        completionPercent: 0,
        turnsToComplete: 1,
      });

      const openToast = vi.fn();
      const result = await withSettlementAction("EstablishCamp", "Establish Camp", openToast);

      await act(async () => result.current.contextActionToolbarActions[0].onClick());

      await waitFor(() => expect(settleForce).toHaveBeenCalledWith({ unitInstanceId: 7, tier: "Camp" }));

      expect(result.current.settlementDialogTier).toBeNull();
      await waitFor(() => expect(openToast).toHaveBeenCalledWith(expect.stringContaining("Your camp founded"), "success"));
    });

    /** There is no dialog to show a camp's refusal in, so the server's own wording goes to the toast instead. */
    it("reports a refused camp in a toast", async () => {
      settleForce.mockRejectedValue(new Error("This ground cannot be settled."));

      const openToast = vi.fn();
      const result = await withSettlementAction("EstablishCamp", "Establish Camp", openToast);

      await act(async () => result.current.contextActionToolbarActions[0].onClick());

      await waitFor(() => expect(openToast).toHaveBeenCalledWith("This ground cannot be settled.", "danger"));
    });

    /**
     * A settlement takes one name and one population, and there is nowhere to put a second of either — so a selection of
     * several settlers is turned away rather than pooled into one settlement or founded from an arbitrary member of it.
     */
    it("refuses to open for a selection of several forces, and says why", async () => {
      vi.mocked(getForceContextActions).mockResolvedValue([
        { actionType: "CreateVillage", label: "Create Village", displayOrder: 1100, isEnabled: true, disabledReasons: [] },
      ]);

      const openToast = vi.fn();
      const { result } = renderForceSelection(openToast);

      const first = force({ id: 7 });
      const second = force({ id: 8 });

      act(() => {
        result.current.selectForceFromTile(tile([first, second]));
      });

      // Ctrl+click (append) adds the second force to the selection the map click started.
      act(() => result.current.chooseForce(second, true));

      expect(selectedIds(result)).toEqual([7, 8]);

      await waitFor(() => expect(result.current.contextActionToolbarActions).toHaveLength(1));

      act(() => result.current.contextActionToolbarActions[0].onClick());

      expect(result.current.settlementDialogTier).toBeNull();
      expect(openToast).toHaveBeenCalledWith(expect.stringContaining("Select a single force"), "info");
    });
  });
});
