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
 * Owns the player's force selection: which forces are selected, which force is shown per tile, and the
 * force-selection bar shown when a tile holds several. Also derives the context-action toolbar actions
 * for the selection.
 */
export function useForceSelection({ gameId, playerId, openToast }: UseForceSelectionOptions) {
  // A list rather than a single force: the selection bar is a multi-select (ctrl/⌘+click). Ordered by the
  // click that added each one, which is what makes "the force just clicked" a meaningful notion below.
  const [selectedForces, setSelectedForces] = useState<ObservedForceTile[]>([]);
  const [visibleForceIdsByTile, setVisibleForceIdsByTile] = useState<Record<string, number>>({});

  // The tile whose forces the selection bar is offering. Set only when the player has more than one force
  // standing on the clicked tile — with a single force there is nothing to choose between.
  const [multiForceTile, setMultiForceTile] = useState<ForceSelectionToolbarTile | null>(null);

  // Reset all force-selection state when the game closes.
  useEffect(() => {
    if (gameId == null) {
      setSelectedForces([]);
      setVisibleForceIdsByTile({});
      setMultiForceTile(null);
    }
  }, [gameId]);

  // Memoised because both the bar and every map tile take it, and Tile is memoised — a fresh Set each render
  // would defeat that and re-render the whole grid on any unrelated state change.
  const selectedForceIds = useMemo<ReadonlySet<number>>(
    () => new Set(selectedForces.map((force) => force.id)),
    [selectedForces],
  );

  // The ids the actions query is keyed on. Sorted here so the identity is stable while the selection's contents
  // are, however the player reordered it by ctrl+clicking things off and on again.
  const selectedForceIdList = useMemo(
    () => [...selectedForceIds].sort((a, b) => a - b),
    [selectedForceIds],
  );

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

  /**
   * Picks a force from the selection bar.
   *
   * A plain click clears the selection and sets it to this force alone. A ctrl/⌘+click appends it, or removes it
   * again when it is already selected — including when it is the last one, which leaves nothing selected. That is
   * why the dock stays mounted while the bar has a tile to show (see GameUI): the player has to be able to click
   * again in the bar they just emptied.
   */
  const chooseForce = useCallback(
    (force: ObservedForceTile, append: boolean = false) => {
      const wasSelected = selectedForces.some((selected) => selected.id === force.id);

      if (!append) {
        setSelectedForces([force]);
      } else if (wasSelected) {
        setSelectedForces((current) => current.filter((selected) => selected.id !== force.id));
      } else {
        setSelectedForces((current) => [...current, force]);
      }

      if (multiForceTile == null) return;

      // Pinned only when the click *selected* the force. Pinning on a removing ctrl+click would leave the tile
      // drawing the very force the player just deselected.
      const isNowSelected = !append || !wasSelected;
      if (!isNowSelected) return;

      setVisibleForceIdsByTile((current) => ({
        ...current,
        [getTileKey(multiForceTile.x, multiForceTile.y)]: force.id,
      }));
    },
    [multiForceTile, selectedForces],
  );

  /** Names the selection for a toast, so a multi-force order reads as one rather than as a mystery. */
  const describeSelection = useCallback(() => {
    return selectedForces.map((force) => `#${force.id}`).join(", ");
  }, [selectedForces]);

  const moveTo = useCallback(
    (tile: TileCell) => {
      if (selectedForces.length === 0) return;

      const noun = selectedForces.length === 1 ? "force" : "forces";
      openToast(`Move ${noun} ${describeSelection()} to [${tile.x}, ${tile.y}]`, "info");
    },
    [describeSelection, openToast, selectedForces],
  );

  // The backend decides which actions apply to the selection and whether each can be taken right now. With
  // several forces selected it judges every one of them: an action is enabled only when all of them can take it.
  const { data: forceContextActions } = useForceContextActionsQuery(selectedForceIdList);

  /**
   * Starts the interaction behind a backend-driven action.
   *
   * The backend deliberately doesn't know what a button *does* — only whether it's allowed — so the mapping from
   * action type to interaction lives here. Whatever the interaction is, it acts on the whole selection.
   */
  const runForceAction = useCallback(
    (actionType: ForceActionType) => {
      if (selectedForces.length === 0) return;

      switch (actionType) {
        case "SplitForce":
          // TODO: Splitting needs parameters (how many ways, and how big each part is), so this opens a dialog
          // rather than posting directly. The dialog and its execute endpoint are the next slice of this work —
          // see docs/design/units/force-context-action-toolbar.md, stage 5. That work reads the selection below
          // rather than the checkbox channel the bar used to carry, which ctrl+click replaced.
          openToast(`Split force ${describeSelection()} — the split dialog isn't built yet.`, "info");
          break;
      }
    },
    [describeSelection, openToast, selectedForces],
  );

  const onTileHold = useCallback(
    (tile: TileCell) => {
      if (selectedForces.length === 0) return;

      // Holding the tile the selection already stands on is not a move. Every selected force shares a tile today,
      // so one match is enough to know the target is where they already are.
      if ((tile.forces ?? []).some((force) => selectedForceIds.has(force.id))) return;

      moveTo(tile);
    },
    [moveTo, selectedForceIds, selectedForces],
  );

  /** Selects the player's force on a clicked tile (single force, multi-force, or none). */
  const selectForceFromTile = useCallback(
    (t: TileCell) => {
      if (playerId == null) return;

      // Only the player's own forces are ever selectable, so an enemy or ally sharing the tile is drawn on the
      // map but never offered in the bar. This filter is what makes that true — the bar shows what it's given.
      const ownedForces = (t.forces ?? []).filter((force) => force.ownerPlayerId === playerId);

      // Clicking the map always replaces the selection, however many forces were in it. Multi-select is the bar's
      // gesture; the map's click is what starts a fresh selection.
      if (ownedForces.length === 1) {
        setSelectedForces([ownedForces[0]]);
        setVisibleForceIdsByTile((current) => ({
          ...current,
          [getTileKey(t.x, t.y)]: ownedForces[0].id,
        }));
        setMultiForceTile(null);
      } else if (ownedForces.length > 1) {
        const displayedOwnedForce = getVisibleForceForTile(t, { forces: ownedForces }) ?? ownedForces[0];

        setSelectedForces([displayedOwnedForce]);
        setVisibleForceIdsByTile((current) => ({
          ...current,
          [getTileKey(t.x, t.y)]: displayedOwnedForce.id,
        }));
        setMultiForceTile({ x: t.x, y: t.y, forces: ownedForces });
      } else {
        setSelectedForces([]);
        setMultiForceTile(null);
      }
    },
    [playerId, getVisibleForceForTile],
  );

  const contextActionToolbarActions = useMemo<ContextActionToolbarAction[]>(() => {
    if (selectedForces.length === 0) return [];

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
  }, [runForceAction, selectedForces, forceContextActions]);

  return {
    selectedForces,
    selectedForceIds,
    multiForceTile,
    getVisibleForceId,
    chooseForce,
    onTileHold,
    selectForceFromTile,
    contextActionToolbarActions,
  };
}
