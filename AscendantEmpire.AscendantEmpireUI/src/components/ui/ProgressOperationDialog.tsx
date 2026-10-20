import { useEffect, useRef } from "react";
import { Alert, Button, Modal, ProgressBar } from "@/components/ui";

/** How long a completed operation stays on screen before the dialog closes itself. */
const AUTO_CLOSE_DELAY_MS = 3000;

export interface ProgressOperationDialogProps {
  open: boolean;
  /** Dialog title shown in the header. */
  title: string;
  /** Short label shown below the progress bar while the operation is running. */
  operationLabel: string;
  /** 0–100; drives the ProgressBar fill. Reaching 100 counts as completion even while status is still "active". */
  progressPercent: number;
  status: "active" | "done" | "error";
  /** Shown inside a success Alert when status === "done". */
  successMessage?: string;
  /** Shown inside a danger Alert when status === "error". */
  errorMessage?: string;
  onClose: () => void;
}

/**
 * Generic progress dialog for long-running background operations.
 * Shows a ProgressBar while active, then a success or error alert when complete.
 *
 * Once the operation completes — the bar reaches 100, or the caller reports "done" — there is nothing
 * left to cancel, so the footer button becomes "Complete" and the dialog closes itself after a short
 * pause. A failure never auto-closes: the error has to be read, and dismissed deliberately.
 */
export default function ProgressOperationDialog({
  open,
  title,
  operationLabel,
  progressPercent,
  status,
  successMessage,
  errorMessage,
  onClose,
}: ProgressOperationDialogProps) {
  const clamped = Math.min(Math.max(progressPercent, 0), 100);

  // Completion is either the end of the caller's state machine ("done") or a full bar while still
  // polling, since a caller that only reports progress has no other way of saying it has finished.
  const isComplete = status === "done" || (status === "active" && clamped >= 100);

  // Callers commonly pass an inline arrow for onClose, which is a new function on every poll tick.
  // Holding it in a ref keeps it out of the timer effect's dependencies, so a re-render mid-countdown
  // cannot restart the timer and strand the dialog open.
  const onCloseRef = useRef(onClose);
  onCloseRef.current = onClose;

  useEffect(() => {
    if (!open || !isComplete) {
      return;
    }

    const timer = window.setTimeout(() => onCloseRef.current(), AUTO_CLOSE_DELAY_MS);

    // Clears on unmount, on close, and if the operation somehow leaves the completed state.
    return () => window.clearTimeout(timer);
  }, [open, isComplete]);

  // "Cancel" is only honest while the operation can still be abandoned; past that the button only dismisses.
  const closeButtonLabel = status === "error" ? "Close" : isComplete ? "Complete" : "Cancel";

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={title}
      size="md"
      footer={
        <Button variant="secondary" onClick={onClose}>
          {closeButtonLabel}
        </Button>
      }>
      <div className="flex flex-col gap-3 py-1">
        {status === "active" && (
          <>
            {/* Full-width wrapper — ProgressBar uses fixed px internally so we stretch it to fill. */}
            <div className="w-full" style={{ lineHeight: 0 }}>
              <ProgressBar
                totalDisplayWidth={440}
                completedAmount={clamped}
                totalAmount={100}
                showPercentage={true}
              />
            </div>
            <p className="text-sm text-slate-600">{operationLabel}</p>
          </>
        )}

        {status === "done" && (
          <Alert variant="success">{successMessage ?? "Operation completed successfully."}</Alert>
        )}

        {status === "error" && (
          <Alert variant="danger">{errorMessage ?? "An error occurred."}</Alert>
        )}
      </div>
    </Modal>
  );
}
