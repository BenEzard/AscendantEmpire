import { Button, Modal } from "@/components/ui";
import ObservedTroop from "@/features/map/components/ObservedTroop";
import type { ObservedTroopTile } from "@/features/map/components/TileTypes";

export interface ForceSelectionDialogTile {
  x: number;
  y: number;
  troops: ObservedTroopTile[];
}

export interface ForceSelectionDialogProps {
  open: boolean;
  tile: ForceSelectionDialogTile | null;
  selectedTroopId?: number;
  troopImageSrcByUnitDesignId?: ReadonlyMap<number, string>;
  onClose: () => void;
  onSelect: (troop: ObservedTroopTile) => void;
}

export default function ForceSelectionDialog({
  open,
  tile,
  selectedTroopId,
  troopImageSrcByUnitDesignId,
  onClose,
  onSelect,
}: ForceSelectionDialogProps) {
  return (
    <Modal
      open={open && tile != null}
      onClose={onClose}
      title={tile ? `Select Force at (${tile.x}, ${tile.y})` : "Select Force"}
      size="md"
      footer={
        <Button variant="secondary" onClick={onClose}>
          Cancel
        </Button>
      }>
      {tile ? (
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
          {tile.troops.map((troop) => (
            <button
              key={troop.id}
              type="button"
              className={[
                "rounded-md border p-2 text-left transition-colors",
                selectedTroopId === troop.id ? "border-yellow-400 bg-yellow-50" : "border-slate-200 hover:bg-slate-50",
              ].join(" ")}
              onClick={() => onSelect(troop)}>
              <div className="relative mx-auto mb-2 h-20 w-20">
                <ObservedTroop
                  troop={troop}
                  troopInstanceCount={1}
                  isSelected={selectedTroopId === troop.id}
                  troopImageSrcByUnitDesignId={troopImageSrcByUnitDesignId}
                />
              </div>
              <div className="text-xs text-slate-600">Troop #{troop.id}</div>
            </button>
          ))}
        </div>
      ) : null}
    </Modal>
  );
}
