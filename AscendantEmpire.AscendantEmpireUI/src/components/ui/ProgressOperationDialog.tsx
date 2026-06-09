import { Alert, Button, Modal, ProgressBar } from "@/components/ui";

export interface ProgressOperationDialogProps {
  open: boolean;
  /** Dialog title shown in the header. */
  title: string;
  /** Short label shown below the progress bar while the operation is running. */
  operationLabel: string;
  /** 0–100; drives the ProgressBar fill. */
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

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={title}
      size="md"
      footer={
        <Button variant="secondary" onClick={onClose}>
          {status === "active" ? "Cancel" : "Close"}
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
