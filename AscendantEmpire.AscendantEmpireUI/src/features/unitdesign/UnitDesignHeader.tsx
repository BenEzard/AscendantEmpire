import Button from "@/components/ui/Button";
import { VscNewFile } from "react-icons/vsc";

export interface UnitDesignHeaderProps {
  onClose: () => void;
  onReset: () => void;
  /** When provided, a Save button is shown (edit mode with a persistence handler). */
  onSave?: () => void;
  /** Disables Save when the current design can't be persisted yet (e.g. no base chosen). */
  canSave?: boolean;
}

export default function UnitDesignHeader({ onClose, onReset, onSave, canSave = true }: UnitDesignHeaderProps) {
  return (
    <div className="flex items-center justify-between border-b border-white/10 px-4 py-2">
      <div className="flex items-center gap-2">
        <div className="text-lg font-semibold text-white">Unit Designer</div>
        <button
          type="button"
          className="rounded-md p-2 text-white hover:bg-white/15 focus:outline-none focus-visible:ring-2 focus-visible:ring-white/30"
          aria-label="Reset to initial view"
          onClick={onReset}>
          <VscNewFile className="h-4 w-4" aria-hidden />
        </button>
      </div>
      <div className="flex items-center gap-2">
        {onSave && (
          <Button variant="primary" onClick={onSave} disabled={!canSave}>
            Save
          </Button>
        )}
        <Button variant="light" onClick={onClose}>
          Close
        </Button>
      </div>
    </div>
  );
}
