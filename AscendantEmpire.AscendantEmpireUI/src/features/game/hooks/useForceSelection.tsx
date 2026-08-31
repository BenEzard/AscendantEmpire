import { useCallback, useEffect, useMemo, useState } from "react";
import { FaChessKnight } from "react-icons/fa6";
import type { ObservedForceTile, TileCell } from "@/features/map/components/TileTypes";
import type { ForceSelectionDialogTile } from "../components/ForceSelectionDialog";
import type { ContextActionToolbarAction } from "../components/ContextActionToolbar";
import { FRONTEND_ONLY_ACTION_ORDER, getForceActionIcon, getForceActionLabel } from "../components/forceActionRegistry";
import { useForceContextActionsQuery } from "./useForceContextActions";
import type { ForceActionType } from "../api/forceContextActionsApi";
import type { ToastVariant } from "@/hooks/useToast";

function getTileKey(x: number, y: number): string {
  return `${x},${y}`;
}

/**
 * Returns the force with the highest number of healthy troops (the default
 * display-on-tile), or null if the array is empty.
 */
function getForceWithMaxHealthy(forces: ObservedForceTile[]): ObservedForceTile | null {
  if (forces.length === 0) return null;

  let strongestForce = forces[0];
  for (const force of forces.slice(1)) {
    if (force.healthyTroops > strongestForce.healthyTroops) {
      strongestForce = force;
    }
  }

  return strongestForce;
}

interface UseForceSelectionOptions {
  gameId: number | undefined;
  playerId: number | undefined;
  openToast: (message: string, variant?: ToastVariant) => void;
}

/**
 * Owns the player's force (force) selection: which force is selected, which force is
 * shown per tile, and the multi-force selection dialog. Also derives the context-action
 * toolbar actions for the selected force.
 */
export function useForceSelection({ gameId, playerId, openToast }: UseForceSelectionOptions) {
  const [selectedForce, setSelectedForce] = useState<ObservedForceTile | null>(null);
  const [visibleForceIdsByTile, setVisibleForceIdsByTile] = useState<Record<string, number>>({});
  const [pendingForceSelectionTile, setPendingForceSelectionTile] = useState<ForceSelectionDialogTile | null>(null);
  const [forceSelectionTile, setForceSelectionTile] = useState<ForceSelectionDialogTile | null>(null);

  // Reset all force-selection state when the game closes.
  useEffect(() => {
    if (gameId == null) {
      setSelectedForce(null);
      setVisibleForceIdsByTile({});
      setPendingForceSelectionTile(null);
      setForceSelectionTile(null);
    }
  }, [gameId]);

  const getVisibleForceForTile = useCallback(
    (tile: TileCell, options?: { forces?: ObservedForceTile[] }) => {
      const forces = options?.forces ?? tile.forces ?? [];
      if (forces.length === 0) return null;

      const visibleForceId = visibleForceIdsByTile[getTileKey(tile.x, tile.y)];
      if (visibleForceId != null) {
        const visibleForce = forces.find((force) => force.id === visibleForceId);
        if (visibleForce) return visibleForce;
      }

      return getForceWithMaxHealthy(forces);
    },
    [visibleForceIdsByTile],
  );

  const getVisibleForceId = useCallback((tile: TileCell) => getVisibleForceForTile(tile)?.id, [getVisibleForceForTile]);

  const closeForceSelectionDialog = useCallback(() => {
    setForceSelectionTile(null);
  }, []);

  const openForceSelectionDialog = useCallback(() => {
    if (pendingForceSelectionTile == null) return;
    setForceSelectionTile(pendingForceSelectionTile);
  }, [pendingForceSelectionTile]);

  const chooseForce = useCallback(
    (force: ObservedForceTile) => {
      setSelectedForce(force);
      if (forceSelectionTile) {
        setVisibleForceIdsByTile((current) => ({
          ...current,
          [getTileKey(forceSelectionTile.x, forceSelectionTile.y)]: force.id,
        }));
        setPendingForceSelectionTile(forceSelectionTile);
      }
      setForceSelectionTile(null);
    },
    [forceSelectionTile],
  );

  // Press "t" to (re)open the multi-force selection dialog for the pending tile.
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
      openToast(`Move force #${selectedForce.id} to [${tile.x}, ${tile.y}]`, "info");
    },
    [openToast, selectedForce],
  );

  // The backend decides which actions apply to the selected force and whether each can be taken right now.
  const { data: forceContextActions } = useForceContextActionsQuery(selectedForce?.id);

  /**
   * Starts the interaction behind a backend-driven action.
   *
   * The backend deliberately doesn't know what a button *does* — only whether it's allowed — so the mapping from
   * action type to interaction lives here.
   */
  const runForceAction = useCallback(
    (actionType: ForceActionType) => {
      if (!selectedForce) return;

      switch (actionType) {
        case "SplitForce":
          // TODO: Splitting needs parameters (how many ways, and how big each part is), so this opens a dialog
          // rather than posting directly. The dialog and its execute endpoint are the next slice of this work —
          // see docs/design/units/force-context-action-toolbar.md, stage 5.
          openToast(`Split force #${selectedForce.id} — the split dialog isn't built yet.`, "info");
          break;
      }
    },
    [openToast, selectedForce],
  );

  const onTileHold = useCallback(
    (tile: TileCell) => {
      if (!selectedForce) return;
      if ((tile.forces ?? []).some((force) => force.id === selectedForce.id)) return;
      moveTo(tile);
    },
    [moveTo, selectedForce],
  );

  /** Selects the player's force on a clicked tile (single force, multi-force, or none). */
  const selectForceFromTile = useCallback(
    (t: TileCell) => {
      if (playerId == null) return;

      const ownedForces = (t.forces ?? []).filter((force) => force.ownerPlayerId === playerId);

      if (ownedForces.length === 1) {
        setSelectedForce(ownedForces[0]);
        setVisibleForceIdsByTile((current) => ({
          ...current,
          [getTileKey(t.x, t.y)]: ownedForces[0].id,
        }));
        setPendingForceSelectionTile(null);
        setForceSelectionTile(null);
      } else if (ownedForces.length > 1) {
        const displayedOwnedForce = getVisibleForceForTile(t, { forces: ownedForces }) ?? ownedForces[0];

        setSelectedForce(displayedOwnedForce);
        setVisibleForceIdsByTile((current) => ({
          ...current,
          [getTileKey(t.x, t.y)]: displayedOwnedForce.id,
        }));
        setPendingForceSelectionTile({ x: t.x, y: t.y, forces: ownedForces });
        setForceSelectionTile(null);
      } else {
        setSelectedForce(null);
        setPendingForceSelectionTile(null);
        setForceSelectionTile(null);
      }
    },
    [playerId, getVisibleForceForTile],
  );

  const contextActionToolbarActions = useMemo<ContextActionToolbarAction[]>(() => {
    if (!selectedForce) return [];

    const actions: ContextActionToolbarAction[] = [];

    // "Select Force" is a pure UI affordance with no backend rule behind it, so it's added here rather than
    // served by the API. Its display order comes from the band the backend's action enum reserves for exactly
    // this, so it sorts into the same sequence without ever colliding with a real action.
    if (pendingForceSelectionTile != null) {
      actions.push({
        key: "select-force",
        label: "Select Force",
        icon: <FaChessKnight />,
        displayOrder: FRONTEND_ONLY_ACTION_ORDER.selectForce,
        isEnabled: true,
        disabledReasons: [],
        onClick: openForceSelectionDialog,
      });
    }

    // Everything else comes from the backend, including the actions it has judged unavailable — those render
    // greyed out with their reasons, rather than being filtered away, so the player learns why.
    for (const action of forceContextActions ?? []) {
      actions.push({
        key: action.actionType,
        label: getForceActionLabel(action.actionType, action.label),
        icon: getForceActionIcon(action.actionType),
        displayOrder: action.displayOrder,
        isEnabled: action.isEnabled,
        disabledReasons: action.disabledReasons,
        onClick: () => runForceAction(action.actionType),
      });
    }

    return actions;
  }, [openForceSelectionDialog, pendingForceSelectionTile, runForceAction, selectedForce, forceContextActions]);

  return {
    selectedForce,
    forceSelectionTile,
    getVisibleForceId,
    closeForceSelectionDialog,
    chooseForce,
    onTileHold,
    selectForceFromTile,
    contextActionToolbarActions,
  };
}
