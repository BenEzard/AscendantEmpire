import { useCallback, useEffect, useMemo, useState } from "react";
import type { ObservedForceTile, TileCell } from "@/features/map/components/TileTypes";
import type { ForceSelectionToolbarTile } from "../components/ForceSelectionToolbar";
import type { ContextActionToolbarAction } from "../components/ContextActionToolbar";
import { getForceActionIcon, getForceActionLabel } from "../components/forceActionRegistry";
import { useForceContextActionsQuery } from "./useForceContextActions";
import type { ForceActionType } from "../api/forceContextActionsApi";
import type { MoveDirection, StepForceResponse } from "../api/forceMovementApi";
import { directionBetween } from "@/features/map/util/mapDirections";
import type { ToastVariant } from "@/hooks/useToast";

/**
 * What holding a tile should do with the current selection.
 *
 * A verdict rather than an action, so the hook stays responsible for what is selected and where it stands, and the caller
 * stays responsible for the request and for what to tell the player.
 */
export type StepPlan =
  | { kind: "step"; unitInstanceId: number; direction: MoveDirection }
  /** Nothing selected — the gesture means nothing, so say nothing. */
  | { kind: "nothing-selected" }
  /** The selection holds several forces, and moving an arbitrary one of them would be a surprise. */
  | { kind: "multiple-selected" }
  /** The held tile is where the force already stands. */
  | { kind: "already-here" }
  /** The held tile is not one of the eight neighbours. */
  | { kind: "too-far" };

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

  // Where each force we've seen selected is standing. ObservedForceTile carries no coordinates of its own — it is what was
  // found *on* a tile — so working out which direction a held tile lies in needs the origin recorded separately. Written
  // wherever a force is picked up (from the map or from the bar) and again when one moves.
  const [tileByForceId, setTileByForceId] = useState<Record<number, { x: number; y: number }>>({});

  // The tile whose forces the selection bar is offering. Set only when the player has more than one force
  // standing on the clicked tile — with a single force there is nothing to choose between.
  const [multiForceTile, setMultiForceTile] = useState<ForceSelectionToolbarTile | null>(null);

  // Reset all force-selection state when the game closes.
  useEffect(() => {
    if (gameId == null) {
      setSelectedForces([]);
      setVisibleForceIdsByTile({});
      setTileByForceId({});
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

  /**
   * Works out what holding a tile should do, given what is selected and where it stands.
   *
   * The caller decides what to say about each outcome and owns the request itself: this hook knows what is selected, not how
   * to talk to the API. Deliberately the same shape of decision the numpad makes, so both gestures obey one rule.
   */
  const getStepToTile = useCallback(
    (tile: TileCell): StepPlan => {
      if (selectedForces.length === 0) return { kind: "nothing-selected" };
      if (selectedForces.length > 1) return { kind: "multiple-selected" };

      const force = selectedForces[0];
      const origin = tileByForceId[force.id];

      // No recorded origin means the force was never picked up from a tile we saw, which shouldn't happen — but guessing a
      // direction from nothing would move it somewhere arbitrary, so do nothing instead.
      if (origin == null) return { kind: "nothing-selected" };

      if (origin.x === tile.x && origin.y === tile.y) return { kind: "already-here" };

      const direction = directionBetween(origin, tile);
      if (direction == null) return { kind: "too-far" };

      return { kind: "step", unitInstanceId: force.id, direction };
    },
    [selectedForces, tileByForceId],
  );

  /**
   * Brings the selection up to date after a force has stepped to a new tile.
   *
   * Everything here is read from the step's own response rather than from cached state. The response describes the map as it
   * is *after* the move, including the destination tile and everything standing on it, so this needs no second read and
   * cannot act on a view of the map from before the force set off.
   */
  const onForceMoved = useCallback(
    (result: StepForceResponse) => {
      const { unitInstanceId, xCoord, yCoord, campaignMovementRemaining } = result;

      const destinationTile = result.tiles.find((tile) => tile.xCoord === xCoord && tile.yCoord === yCoord);
      const forcesOnDestination = destinationTile?.forces ?? [];

      // The server's own account of the force after the step. Preferred over patching the snapshot we hold, because it is
      // current in every field rather than just the one we happened to know had changed.
      const movedForce = forcesOnDestination.find((force) => force.id === unitInstanceId);

      setSelectedForces((current) =>
        current.map((force) =>
          force.id === unitInstanceId
            ? // Falls back to patching movement only, for the case where refreshing the player's view of the map failed and
              // the response carries no tiles. The step itself still happened, so the bar must still show it was paid for.
              (movedForce ?? { ...force, campaignMovementRemaining, hasCampaignMovementRemaining: campaignMovementRemaining > 0 })
            : force,
        ),
      );

      // The tile pin follows the force to its destination so that tile draws it. The stale pin on the tile it left needs no
      // clearing: getVisibleForceForTile falls back to the strongest force present when the pinned id isn't there any more.
      setVisibleForceIdsByTile((current) => ({ ...current, [getTileKey(xCoord, yCoord)]: unitInstanceId }));

      // Recorded origins have to follow the force, or a second hold would be judged against where it used to stand. Every
      // owned force on the destination is recorded, not just the mover, so one picked from the bar can be moved straight on.
      setTileByForceId((current) => {
        const next = { ...current, [unitInstanceId]: { x: xCoord, y: yCoord } };
        for (const force of forcesOnDestination) {
          if (force.ownerPlayerId === playerId) {
            next[force.id] = { x: xCoord, y: yCoord };
          }
        }
        return next;
      });

      // The bar belongs to wherever the force now stands, not where it set off from — otherwise moving off a crowded tile
      // leaves the bar offering that tile's forces, including the one that just left it. Same rule as selecting a tile by
      // hand: it is only worth showing when there is more than one force to choose between.
      const ownedOnDestination = forcesOnDestination.filter((force) => force.ownerPlayerId === playerId);
      setMultiForceTile(ownedOnDestination.length > 1 ? { x: xCoord, y: yCoord, forces: ownedOnDestination } : null);
    },
    [playerId],
  );

  /**
   * Steps the selected force one tile, when there is exactly one selected.
   *
   * Returns the force to move, or null when the press should be ignored. The caller owns the request itself: this hook knows
   * what is selected, not how to talk to the API.
   */
  const getForceToStep = useCallback(() => {
    return selectedForces.length === 1 ? selectedForces[0] : null;
  }, [selectedForces]);

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

        case "CreateVillage":
          // TODO: There is no Settlement entity in the model at all yet — settlements.md names the tiers and
          // nothing more — so there is nothing to post to. The backend already judges whether the action is
          // allowed; what's missing is the thing it would create. See docs/plans/force-instances-roadmap.md,
          // Phase 10 (POST /force-orders/settle).
          openToast(`Create a village with ${describeSelection()} — settlements aren't built yet.`, "info");
          break;

        case "EstablishCamp":
          // TODO: Same gap as CreateVillage above — a camp is the smallest settlement tier, and the tier itself
          // doesn't exist yet.
          openToast(`Establish a camp with ${describeSelection()} — settlements aren't built yet.`, "info");
          break;
      }
    },
    [describeSelection, openToast, selectedForces],
  );

  /** Selects the player's force on a clicked tile (single force, multi-force, or none). */
  const selectForceFromTile = useCallback(
    (t: TileCell) => {
      if (playerId == null) return;

      // Only the player's own forces are ever selectable, so an enemy or ally sharing the tile is drawn on the
      // map but never offered in the bar. This filter is what makes that true — the bar shows what it's given.
      const ownedForces = (t.forces ?? []).filter((force) => force.ownerPlayerId === playerId);

      // Record where these forces stand. This is the only place the map tells us a force's position, and holding a tile
      // later needs it to work out which direction that tile lies in. Recorded for every owned force on the tile, not just
      // the one selected, so ctrl+clicking a different one in the bar finds an origin already waiting.
      if (ownedForces.length > 0) {
        setTileByForceId((current) => {
          const next = { ...current };
          for (const force of ownedForces) {
            next[force.id] = { x: t.x, y: t.y };
          }
          return next;
        });
      }

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
    selectForceFromTile,
    contextActionToolbarActions,
    getForceToStep,
    getStepToTile,
    onForceMoved,
  };
}
