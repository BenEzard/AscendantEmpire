import { ProgressOperationDialog } from "@/components/ui";
import type { ImportPhase } from "../hooks/useImportCampaignMap";

export interface ImportProgressDialogProps {
  open: boolean;
  fileName: string;
  importPhase: ImportPhase;
  progressPercent: number;
  onClose: () => void;
}

/** Maps the import phase state machine onto the generic ProgressOperationDialog. */
export default function ImportProgressDialog({
  open,
  fileName,
  importPhase,
  progressPercent,
  onClose,
}: ImportProgressDialogProps) {
  const phaseLabel: Record<ImportPhase["phase"], string> = {
    idle: "",
    starting: "Starting import…",
    polling: "Import in progress…",
    done: "",
    error: "",
  };

  const status =
    importPhase.phase === "done"
      ? "done"
      : importPhase.phase === "error"
        ? "error"
        : "active";

  return (
    <ProgressOperationDialog
      open={open}
      title={fileName ? `Importing "${fileName}"` : "Importing map"}
      operationLabel={phaseLabel[importPhase.phase]}
      progressPercent={progressPercent}
      status={status}
      successMessage={`Import complete. The new map has been added to the gallery.`}
      errorMessage={importPhase.phase === "error" ? importPhase.message : undefined}
      onClose={onClose}
    />
  );
}
