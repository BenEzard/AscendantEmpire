import { useCallback, useEffect, useMemo, useState } from "react";
import type { ObservedForceTile, TileCell } from "@/features/map/components/TileTypes";
import type { ForceSelectionToolbarTile } from "../components/ForceSelectionToolbar";
import type { ContextActionToolbarAction } from "../components/ContextActionToolbar";
import { getForceActionIcon, getForceActionLabel } from "../components/forceActionRegistry";
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
 * Owns the player's force selection: which force is selected, which force is shown per tile, and the
 * force-selection bar shown when a tile holds several. Also derives the context-action toolbar actions
 * for the selected force.
 */
export function useForceSelection({ gameId, playerId, openToast }: UseForceSelectionOptions) {
  const [selectedForce, setSelectedForce] = useState<ObservedForceTile | null>(null);
  const [visibleForceIdsByTile, setVisibleForceIdsByTile] = useState<Record<string, number>>({});

  // The tile whose forces the selection bar is offering. Set only when the player has more than one force
  // standing on the clicked tile — with a single force there is nothing to choose between.
  const [multiForceTile, setMultiForceTile] = useState<ForceSelectionToolbarTile | null>(null);

  // Multi-select mode for the bar. Nothing turns this on yet: it is the channel split and join (TM-4) will
  // drive, and the checked set is what those will act on. Kept here rather than inside the bar so the bar
  // stays a presenter and TM-4 can read the ticks without lifting state out of it first.
  const [forceSelectionRequired, setForceSelectionRequired] = useState(false);
  const [checkedForceIds, setCheckedForceIds] = useState<ReadonlySet<number>>(() => new Set());

  // Reset all force-selection state when the game closes.
  useEffect(() => {
    if (gameId == null) {
      setSelectedForce(null);
      setVisibleForceIdsByTile({});
      setMultiForceTile(null);
      setForceSelectionRequired(false);
      setCheckedForceIds(new Set());
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

  /** Picks a force from the selection bar. Also makes it the one drawn on its tile. */
  const chooseForce = useCallback(
    (force: ObservedForceTile) => {
      setSelectedForce(force);
      if (multiForceTile == null) return;

      setVisibleForceIdsByTile((current) => ({
        ...current,
        [getTileKey(multiForceTile.x, multiForceTile.y)]: force.id,
      }));
    },
    [multiForceTile],
  );

  const toggleForceChecked = useCallback((forceId: number, checked: boolean) => {
    setCheckedForceIds((current) => {
      const next = new Set(current);
      if (checked) {
        next.add(forceId);
      } else {
        next.delete(forceId);
      }
      return next;
    });
  }, []);

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
          // see docs/design/units/force-context-action-toolbar.md, stage 5. That work is also what will turn on
          // forceSelectionRequired, putting checkboxes on the selection bar.
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

      // Only the player's own forces are ever selectable, so an enemy or ally sharing the tile is drawn on the
      // map but never offered in the bar. This filter is what makes that true — the bar shows what it's given.
      const ownedForces = (t.forces ?? []).filter((force) => force.ownerPlayerId === playerId);

      // Cleared here rather than in an effect on multiForceTile: an effect would fire a second render pass and
      // briefly show the previous tile's ticks against the new tile's forces.
      setCheckedForceIds(new Set());
      setForceSelectionRequired(false);

      if (ownedForces.length === 1) {
        setSelectedForce(ownedForces[0]);
        setVisibleForceIdsByTile((current) => ({
          ...current,
          [getTileKey(t.x, t.y)]: ownedForces[0].id,
        }));
        setMultiForceTile(null);
      } else if (ownedForces.length > 1) {
        const displayedOwnedForce = getVisibleForceForTile(t, { forces: ownedForces }) ?? ownedForces[0];

        setSelectedForce(displayedOwnedForce);
        setVisibleForceIdsByTile((current) => ({
          ...current,
          [getTileKey(t.x, t.y)]: displayedOwnedForce.id,
        }));
        setMultiForceTile({ x: t.x, y: t.y, forces: ownedForces });
      } else {
        setSelectedForce(null);
        setMultiForceTile(null);
      }
    },
    [playerId, getVisibleForceForTile],
  );

  const contextActionToolbarActions = useMemo<ContextActionToolbarAction[]>(() => {
    if (!selectedForce) return [];

    // Every action comes from the backend now, including the ones it has judged unavailable — those render greyed
    // out with their reasons, rather than being filtered away, so the player learns why. Choosing between forces
    // on a tile used to be a frontend-only button here; it is the selection bar's job instead.
    return (forceContextActions ?? []).map((action) => ({
      key: action.actionType,
      label: getForceActionLabel(action.actionType, action.label),
      icon: getForceActionIcon(action.actionType),
      displayOrder: action.displayOrder,
      isEnabled: action.isEnabled,
      disabledReasons: action.disabledReasons,
      onClick: () => runForceAction(action.actionType),
    }));
  }, [runForceAction, selectedForce, forceContextActions]);

  return {
    selectedForce,
    multiForceTile,
    getVisibleForceId,
    chooseForce,
    onTileHold,
    selectForceFromTile,
    contextActionToolbarActions,
    forceSelectionRequired,
    checkedForceIds,
    toggleForceChecked,
  };
}
