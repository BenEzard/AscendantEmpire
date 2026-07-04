import Button from "@/components/ui/Button";
import { useState } from "react";
import { VscNewFile } from "react-icons/vsc";

export interface UnitDesignHeaderProps {
  onClose: () => void;
  onReset: () => void;
  /**
   * When provided, a Save button is shown (edit mode with a persistence handler). Clicking Save opens
   * an inline textbox (prefilled with `defaultDisplayName`) rather than saving immediately; this is
   * called with the confirmed name once the player accepts it.
   */
  onSave?: (displayName: string) => void;
  /** Disables Save when the current design can't be persisted yet (e.g. no base chosen). */
  canSave?: boolean;
  /** Prefilled into the naming textbox when Save is clicked (e.g. the chosen base's display name). */
  defaultDisplayName?: string;
  /** True while a save request is in flight — disables Save/Confirm and shows a busy label. */
  saving?: boolean;
}

export default function UnitDesignHeader({
  onClose,
  onReset,
  onSave,
  canSave = true,
  defaultDisplayName = "",
  saving = false,
}: UnitDesignHeaderProps) {
  // Clicking Save doesn't persist immediately — it opens this inline textbox (prefilled with
  // defaultDisplayName) so the player can confirm or edit the template's name first.
  const [naming, setNaming] = useState(false);
  const [displayName, setDisplayName] = useState(defaultDisplayName);

  const openNaming = () => {
    setDisplayName(defaultDisplayName);
    setNaming(true);
  };

  const cancelNaming = () => setNaming(false);

  const confirmSave = () => {
    const trimmed = displayName.trim();
    if (!trimmed) {
      return;
    }
    onSave?.(trimmed);
    setNaming(false);
  };

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
        {onSave && naming && (
          <input
            autoFocus
            className="rounded-md border border-white/20 bg-white/10 px-2 py-1 text-sm text-white placeholder-white/50 focus:outline-none focus-visible:ring-2 focus-visible:ring-white/30"
            value={displayName}
            onChange={(e) => setDisplayName(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter") {
                confirmSave();
              }
              if (e.key === "Escape") {
                cancelNaming();
              }
            }}
            placeholder="Template name"
            maxLength={100}
            disabled={saving}
          />
        )}
        {onSave && naming && (
          <Button variant="light" onClick={cancelNaming} disabled={saving}>
            Cancel
          </Button>
        )}
        {onSave && (
          <Button
            variant="primary"
            onClick={naming ? confirmSave : openNaming}
            disabled={naming ? displayName.trim().length === 0 || saving : !canSave || saving}>
            {saving ? "Saving…" : naming ? "Confirm" : "Save"}
          </Button>
        )}
        <Button variant="light" onClick={onClose}>
          Close
        </Button>
      </div>
    </div>
  );
}
