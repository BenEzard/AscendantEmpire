import { ProgressOperationDialog } from "@/components/ui";
import type { ExportPhase } from "../hooks/useExportCampaignMap";

export interface ExportProgressDialogProps {
  open: boolean;
  mapName: string;
  exportPhase: ExportPhase;
  progressPercent: number;
  onClose: () => void;
}

/** Maps the export phase state machine onto the generic ProgressOperationDialog. */
export default function ExportProgressDialog({
  open,
  mapName,
  exportPhase,
  progressPercent,
  onClose,
}: ExportProgressDialogProps) {
  const phaseLabel: Record<ExportPhase["phase"], string> = {
    idle: "",
    starting: "Starting export…",
    polling: "Export in progress…",
    downloading: "Preparing download…",
    done: "",
    error: "",
  };

  const status =
    exportPhase.phase === "done"
      ? "done"
      : exportPhase.phase === "error"
        ? "error"
        : "active";

  return (
    <ProgressOperationDialog
      open={open}
      title={`Exporting "${mapName}"`}
      operationLabel={phaseLabel[exportPhase.phase]}
      progressPercent={progressPercent}
      status={status}
      successMessage={`Export complete. Your download has started.`}
      errorMessage={exportPhase.phase === "error" ? exportPhase.message : undefined}
      onClose={onClose}
    />
  );
}
