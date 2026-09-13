import Modal from "@/components/ui/Modal";

interface BattleDialogProps {
  /** Whether the dialog is open. */
  show: boolean;
  /** The force whose Battle button opened the dialog, or null when it is closed. */
  forceId: number | null;
  onClose: () => void;
}

/**
 * The Battle a force is fighting in, opened from the Battle button on its context action toolbar.
 *
 * A placeholder: what this dialog shows, and what the player can do from it, is still to be designed (COMM-1). It exists now
 * so the button has somewhere to go and the wiring from the toolbar to a dialog is in place for whoever designs it.
 */
export default function BattleDialog({ show, forceId, onClose }: BattleDialogProps) {
  return (
    <Modal open={show} title="Battle" onClose={onClose} panelClassName="thick-border-modal">
      {/* The force id is shown so the placeholder at least confirms which force's battle was asked for. */}
      <p>{forceId == null ? "This force is in battle." : `Force #${forceId} is in battle.`}</p>
      <p className="mt-2 text-slate-600">The battle dialog is still to be designed.</p>
    </Modal>
  );
}
