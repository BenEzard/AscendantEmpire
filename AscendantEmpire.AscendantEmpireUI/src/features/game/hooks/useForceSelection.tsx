import { useCallback, useEffect, useMemo, useState } from "react";
import { FaChessKnight } from "react-icons/fa6";
import type { ObservedForceTile, TileCell } from "@/features/map/components/TileTypes";
import type { ForceSelectionDialogTile } from "../components/ForceSelectionDialog";
import type { ContextActionToolbarAction } from "../components/ContextActionToolbar";
import { FRONTEND_ONLY_ACTION_ORDER, getTroopActionIcon, getTroopActionLabel } from "../components/troopActionRegistry";
import { useTroopContextActionsQuery } from "./useTroopContextActions";
import type { TroopActionType } from "../api/troopContextActionsApi";
import type { ToastVariant } from "@/hooks/useToast";

function getTileKey(x: number, y: number): string {
  return `${x},${y}`;
}

/**
 * Returns the force with the highest number of healthy troops (the default
 * display-on-tile), or null if the array is empty.
 */
function getTroopWithMaxHealthy(forces: ObservedForceTile[]): ObservedForceTile | null {
  if (forces.length === 0) return null;

  let strongestTroop = forces[0];
  for (const troop of forces.slice(1)) {
    if (troop.healthyTroops > strongestTroop.healthyTroops) {
      strongestTroop = troop;
    }
  }

  return strongestTroop;
}

interface UseForceSelectionOptions {
  gameId: number | undefined;
  playerId: number | undefined;
  openToast: (message: string, variant?: ToastVariant) => void;
}

/**
 * Owns the player's force (troop) selection: which force is selected, which troop is
 * shown per tile, and the multi-troop selection dialog. Also derives the context-action
 * toolbar actions for the selected force.
 */
export function useForceSelection({ gameId, playerId, openToast }: UseForceSelectionOptions) {
  const [selectedForce, setSelectedForce] = useState<ObservedForceTile | null>(null);
  const [visibleTroopIdsByTile, setVisibleTroopIdsByTile] = useState<Record<string, number>>({});
  const [pendingForceSelectionTile, setPendingForceSelectionTile] = useState<ForceSelectionDialogTile | null>(null);
  const [forceSelectionTile, setForceSelectionTile] = useState<ForceSelectionDialogTile | null>(null);

  // Reset all force-selection state when the game closes.
  useEffect(() => {
    if (gameId == null) {
      setSelectedForce(null);
      setVisibleTroopIdsByTile({});
      setPendingForceSelectionTile(null);
      setForceSelectionTile(null);
    }
  }, [gameId]);

  const getVisibleTroopForTile = useCallback(
    (tile: TileCell, options?: { forces?: ObservedForceTile[] }) => {
      const forces = options?.forces ?? tile.forces ?? [];
      if (forces.length === 0) return null;

      const visibleTroopId = visibleTroopIdsByTile[getTileKey(tile.x, tile.y)];
      if (visibleTroopId != null) {
        const visibleTroop = forces.find((troop) => troop.id === visibleTroopId);
        if (visibleTroop) return visibleTroop;
      }

      return getTroopWithMaxHealthy(forces);
    },
    [visibleTroopIdsByTile],
  );

  const getVisibleTroopId = useCallback((tile: TileCell) => getVisibleTroopForTile(tile)?.id, [getVisibleTroopForTile]);

  const closeForceSelectionDialog = useCallback(() => {
    setForceSelectionTile(null);
  }, []);

  const openForceSelectionDialog = useCallback(() => {
    if (pendingForceSelectionTile == null) return;
    setForceSelectionTile(pendingForceSelectionTile);
  }, [pendingForceSelectionTile]);

  const chooseForce = useCallback(
    (troop: ObservedForceTile) => {
      setSelectedForce(troop);
      if (forceSelectionTile) {
        setVisibleTroopIdsByTile((current) => ({
          ...current,
          [getTileKey(forceSelectionTile.x, forceSelectionTile.y)]: troop.id,
        }));
        setPendingForceSelectionTile(forceSelectionTile);
      }
      setForceSelectionTile(null);
    },
    [forceSelectionTile],
  );

  // Press "t" to (re)open the multi-troop selection dialog for the pending tile.
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key.toLowerCase() !== "t") return;
      if (pendingForceSelectionTile == null) return;

      const target = event.target;
      if (target instanceof HTMLElement) {
        const tagName = target.tagName;
        const isEditable = target.isContentEditable || tagName === "INPUT" || tagName === "TEXTAREA" || tagName === "SELECT";
        if (isEditable) return;
      }

      setForceSelectionTile(pendingForceSelectionTile);
    };

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [pendingForceSelectionTile]);

  const moveTo = useCallback(
    (tile: TileCell) => {
      if (!selectedForce) return;
      openToast(`Move troop #${selectedForce.id} to [${tile.x}, ${tile.y}]`, "info");
    },
    [openToast, selectedForce],
  );

  // The backend decides which actions apply to the selected troop and whether each can be taken right now.
  const { data: troopContextActions } = useTroopContextActionsQuery(selectedForce?.id);

  /**
   * Starts the interaction behind a backend-driven action.
   *
   * The backend deliberately doesn't know what a button *does* — only whether it's allowed — so the mapping from
   * action type to interaction lives here.
   */
  const runTroopAction = useCallback(
    (actionType: TroopActionType) => {
      if (!selectedForce) return;

      switch (actionType) {
        case "SplitTroops":
          // TODO: Splitting needs parameters (how many ways, and how big each part is), so this opens a dialog
          // rather than posting directly. The dialog and its execute endpoint are the next slice of this work —
          // see docs/design/units/troop-context-action-toolbar.md, stage 5.
          openToast(`Split troop #${selectedForce.id} — the split dialog isn't built yet.`, "info");
          break;
      }
    },
    [openToast, selectedForce],
  );

  const onTileHold = useCallback(
    (tile: TileCell) => {
      if (!selectedForce) return;
      if ((tile.forces ?? []).some((troop) => troop.id === selectedForce.id)) return;
      moveTo(tile);
    },
    [moveTo, selectedForce],
  );

  /** Selects the player's force on a clicked tile (single troop, multi-troop, or none). */
  const selectForceFromTile = useCallback(
    (t: TileCell) => {
      if (playerId == null) return;

      const ownedTroops = (t.forces ?? []).filter((troop) => troop.ownerPlayerId === playerId);

      if (ownedTroops.length === 1) {
        setSelectedForce(ownedTroops[0]);
        setVisibleTroopIdsByTile((current) => ({
          ...current,
          [getTileKey(t.x, t.y)]: ownedTroops[0].id,
        }));
        setPendingForceSelectionTile(null);
        setForceSelectionTile(null);
      } else if (ownedTroops.length > 1) {
        const displayedOwnedTroop = getVisibleTroopForTile(t, { forces: ownedTroops }) ?? ownedTroops[0];

        setSelectedForce(displayedOwnedTroop);
        setVisibleTroopIdsByTile((current) => ({
          ...current,
          [getTileKey(t.x, t.y)]: displayedOwnedTroop.id,
        }));
        setPendingForceSelectionTile({ x: t.x, y: t.y, forces: ownedTroops });
        setForceSelectionTile(null);
      } else {
        setSelectedForce(null);
        setPendingForceSelectionTile(null);
        setForceSelectionTile(null);
      }
    },
    [playerId, getVisibleTroopForTile],
  );

  const contextActionToolbarActions = useMemo<ContextActionToolbarAction[]>(() => {
    if (!selectedForce) return [];

    const actions: ContextActionToolbarAction[] = [];

    // "Select Troop" is a pure UI affordance with no backend rule behind it, so it's added here rather than
    // served by the API. Its display order comes from the band the backend's action enum reserves for exactly
    // this, so it sorts into the same sequence without ever colliding with a real action.
    if (pendingForceSelectionTile != null) {
      actions.push({
        key: "select-troop",
        label: "Select Troop",
        icon: <FaChessKnight />,
        displayOrder: FRONTEND_ONLY_ACTION_ORDER.selectTroop,
        isEnabled: true,
        disabledReasons: [],
        onClick: openForceSelectionDialog,
      });
    }

    // Everything else comes from the backend, including the actions it has judged unavailable — those render
    // greyed out with their reasons, rather than being filtered away, so the player learns why.
    for (const action of troopContextActions ?? []) {
      actions.push({
        key: action.actionType,
        label: getTroopActionLabel(action.actionType, action.label),
        icon: getTroopActionIcon(action.actionType),
        displayOrder: action.displayOrder,
        isEnabled: action.isEnabled,
        disabledReasons: action.disabledReasons,
        onClick: () => runTroopAction(action.actionType),
      });
    }

    return actions;
  }, [openForceSelectionDialog, pendingForceSelectionTile, runTroopAction, selectedForce, troopContextActions]);

  return {
    selectedForce,
    forceSelectionTile,
    getVisibleTroopId,
    closeForceSelectionDialog,
    chooseForce,
    onTileHold,
    selectForceFromTile,
    contextActionToolbarActions,
  };
}
