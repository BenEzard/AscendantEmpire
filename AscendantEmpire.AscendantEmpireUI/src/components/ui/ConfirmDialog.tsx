import type { ReactNode } from "react";

import Modal from "./Modal";
import Button from "./Button";
import Alert from "./Alert";
import type { ButtonVariant } from "./Button";

export interface ConfirmDialogProps {
  open: boolean;
  /** Names what is being confirmed, e.g. "Delete game". */
  title: ReactNode;
  /** The body of the question. A node rather than a string so a caller can emphasise the thing at stake. */
  message: ReactNode;
  /** Label on the confirming button. Say what it will do — "Delete", "Discard" — rather than "OK". */
  confirmLabel?: string;
  cancelLabel?: string;
  /** Styling of the confirming button. Defaults to danger, since that is what a confirmation is usually for. */
  confirmVariant?: ButtonVariant;
  /** Shown above the message when the previous attempt failed, so the dialog can stay open with the error. */
  error?: string | null;
  /** True while the confirmed action is in flight, so both buttons can be disabled. */
  busy?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

/**
 * Asks the user to confirm an action before it happens.
 *
 * Built on {@link Modal}, so it inherits the portal, the Escape key, the focus move and the click-outside — all of
 * which `window.confirm` gives up in exchange for an alert box that cannot be styled or read by a test. Use it for
 * anything irreversible; a reversible action does not need a dialog at all.
 */
export default function ConfirmDialog({
  open,
  title,
  message,
  confirmLabel = "Confirm",
  cancelLabel = "Cancel",
  confirmVariant = "danger",
  error = null,
  busy = false,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  return (
    <Modal
      open={open}
      // Closing by Escape or by clicking away is a cancel: the safe answer is the one that happens by accident.
      onClose={onCancel}
      title={title}
      size="md"
      footer={
        <>
          <Button variant="secondary" onClick={onCancel} disabled={busy}>
            {cancelLabel}
          </Button>
          <Button variant={confirmVariant} onClick={onConfirm} disabled={busy}>
            {busy ? "Working..." : confirmLabel}
          </Button>
        </>
      }>
      {error && (
        <Alert variant="danger" className="mb-3">
          {error}
        </Alert>
      )}

      <div className="text-sm text-slate-700">{message}</div>
    </Modal>
  );
}
