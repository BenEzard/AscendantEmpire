import { useEffect, useRef } from "react";
import { ProgressOperationDialog } from "@/components/ui";
import { useProcessStatus } from "../hooks/useProcessStatus";
import type { ProcessingStatusDto } from "../api/processStatusApi";

export interface ProcessProgressDialogProps {
  open: boolean;
  /** Dialog title shown in the header. */
  title: string;
  /** Label shown beneath the progress bar while the process runs. */
  activeLabel: string;
  /** Message shown in the success alert when the process completes. */
  successMessage: string;
  /** The process id to poll. Polling only runs while the dialog is open. */
  processId: number | null;
  onClose: () => void;
  /** Fired once, when the process transitions to a successful completion. */
  onComplete?: (data: ProcessingStatusDto) => void;
}

/**
 * Generic progress dialog driven by useProcessStatus. Polls a process id and renders
 * the shared ProgressOperationDialog. Reusable for any background ProcessType.
 */
export default function ProcessProgressDialog({
  open,
  title,
  activeLabel,
  successMessage,
  processId,
  onClose,
  onComplete,
}: ProcessProgressDialogProps) {
  // Only poll while the dialog is open.
  const { phase, progressPercent, errorMessage, data } = useProcessStatus(open ? processId : null);

  // Ensure onComplete fires at most once per open session.
  const completeFiredRef = useRef(false);

  useEffect(() => {
    if (!open) {
      // Reset so the next time the dialog opens it can fire again.
      completeFiredRef.current = false;
      return;
    }

    if (phase === "done" && !completeFiredRef.current) {
      completeFiredRef.current = true;
      if (data) {
        onComplete?.(data);
      }
    }
  }, [open, phase, data, onComplete]);

  const status = phase === "done" ? "done" : phase === "error" ? "error" : "active";

  return (
    <ProgressOperationDialog
      open={open}
      title={title}
      operationLabel={activeLabel}
      progressPercent={progressPercent}
      status={status}
      successMessage={successMessage}
      errorMessage={errorMessage ?? undefined}
      onClose={onClose}
    />
  );
}
