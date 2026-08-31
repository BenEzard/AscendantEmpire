import { Button, Modal } from "@/components/ui";
import ObservedForce from "@/features/map/components/ObservedForce";
import type { ObservedForceTile } from "@/features/map/components/TileTypes";

export interface ForceSelectionDialogTile {
  x: number;
  y: number;
  forces: ObservedForceTile[];
}

export interface ForceSelectionDialogProps {
  open: boolean;
  tile: ForceSelectionDialogTile | null;
  selectedForceId?: number;
  unitImageSrcByUnitTemplateId?: ReadonlyMap<number, string>;
  onClose: () => void;
  onSelect: (force: ObservedForceTile) => void;
}

export default function ForceSelectionDialog({
  open,
  tile,
  selectedForceId,
  unitImageSrcByUnitTemplateId,
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
          {tile.forces.map((force) => (
            <button
              key={force.id}
              type="button"
              className={[
                "rounded-md border p-2 text-left transition-colors",
                selectedForceId === force.id ? "border-yellow-400 bg-yellow-50" : "border-slate-200 hover:bg-slate-50",
              ].join(" ")}
              onClick={() => onSelect(force)}>
              <div className="relative mx-auto mb-2 h-20 w-20">
                <ObservedForce
                  force={force}
                  unitInstanceCount={1}
                  isSelected={selectedForceId === force.id}
                  unitImageSrcByUnitTemplateId={unitImageSrcByUnitTemplateId}
                />
              </div>
              <div className="text-xs text-slate-600">Force #{force.id}</div>
            </button>
          ))}
        </div>
      ) : null}
    </Modal>
  );
}
