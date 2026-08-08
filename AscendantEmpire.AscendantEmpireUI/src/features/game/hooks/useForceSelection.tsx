import { useCallback, useEffect, useMemo, useState } from "react";
import { FaChessKnight } from "react-icons/fa6";
import { GiHuntingHorn } from "react-icons/gi";
import type { ObservedTroopTile, TileCell } from "@/features/map/components/TileTypes";
import type { ForceSelectionDialogTile } from "../components/ForceSelectionDialog";
import type { ContextActionToolbarAction } from "../components/ContextActionToolbar";
import type { ToastVariant } from "@/hooks/useToast";

function getTileKey(x: number, y: number): string {
  return `${x},${y}`;
}

/**
 * Returns the troop with the highest number of healthy troops (the default
 * display-on-tile), or null if the array is empty.
 */
function getTroopWithMaxHealthy(troops: ObservedTroopTile[]): ObservedTroopTile | null {
  if (troops.length === 0) return null;

  let strongestTroop = troops[0];
  for (const troop of troops.slice(1)) {
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
  const [selectedForce, setSelectedForce] = useState<ObservedTroopTile | null>(null);
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
    (tile: TileCell, options?: { troops?: ObservedTroopTile[] }) => {
      const troops = options?.troops ?? tile.troops ?? [];
      if (troops.length === 0) return null;

      const visibleTroopId = visibleTroopIdsByTile[getTileKey(tile.x, tile.y)];
      if (visibleTroopId != null) {
        const visibleTroop = troops.find((troop) => troop.id === visibleTroopId);
        if (visibleTroop) return visibleTroop;
      }

      return getTroopWithMaxHealthy(troops);
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
    (troop: ObservedTroopTile) => {
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

  const hunt = useCallback(() => {
    if (!selectedForce) return;
    openToast(`Hunt with troop #${selectedForce.id}`, "info");
  }, [openToast, selectedForce]);

  const onTileHold = useCallback(
    (tile: TileCell) => {
      if (!selectedForce) return;
      if ((tile.troops ?? []).some((troop) => troop.id === selectedForce.id)) return;
      moveTo(tile);
    },
    [moveTo, selectedForce],
  );

  /** Selects the player's force on a clicked tile (single troop, multi-troop, or none). */
  const selectForceFromTile = useCallback(
    (t: TileCell) => {
      if (playerId == null) return;

      const ownedTroops = (t.troops ?? []).filter((troop) => troop.commanderId === playerId);

      if (ownedTroops.length === 1) {
        setSelectedForce(ownedTroops[0]);
        setVisibleTroopIdsByTile((current) => ({
          ...current,
          [getTileKey(t.x, t.y)]: ownedTroops[0].id,
        }));
        setPendingForceSelectionTile(null);
        setForceSelectionTile(null);
      } else if (ownedTroops.length > 1) {
        const displayedOwnedTroop = getVisibleTroopForTile(t, { troops: ownedTroops }) ?? ownedTroops[0];

        setSelectedForce(displayedOwnedTroop);
        setVisibleTroopIdsByTile((current) => ({
          ...current,
          [getTileKey(t.x, t.y)]: displayedOwnedTroop.id,
        }));
        setPendingForceSelectionTile({ x: t.x, y: t.y, troops: ownedTroops });
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
    const hasPendingMultiTroopSelection = pendingForceSelectionTile != null;

    if (hasPendingMultiTroopSelection) {
      actions.push({
        key: "select-troop",
        label: "Select Troop",
        icon: <FaChessKnight />,
        onClick: openForceSelectionDialog,
      });
    }

    // TODO: The Hunt action was gated on legacy unit design id 2, which no longer exists now that troops
    // reference unit templates (whose ids are database-dependent, so a magic number can't stand in).
    // Re-enable via a compiled capability tag (e.g. "hunting") once capability grants exist — see
    // docs/design/unit-design/tags-and-modifiers.md, concept 3.
    const canHunt = false;

    if (!hasPendingMultiTroopSelection && canHunt) {
      actions.push({
        key: "hunt",
        label: "Hunt",
        icon: <GiHuntingHorn />,
        onClick: hunt,
      });
    }

    return actions;
  }, [hunt, openForceSelectionDialog, pendingForceSelectionTile, selectedForce]);

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
