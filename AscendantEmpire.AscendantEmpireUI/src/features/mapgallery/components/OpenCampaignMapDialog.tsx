import { useEffect, useRef, useState } from "react";
import { Button, Modal } from "@/components/ui";
import { CiExport, CiImport } from "react-icons/ci";
import ExportProgressDialog from "./ExportProgressDialog";
import ImportProgressDialog from "./ImportProgressDialog";
import { useExportCampaignMap } from "../hooks/useExportCampaignMap";
import { useImportCampaignMap } from "../hooks/useImportCampaignMap";
import type { CampaignMapGalleryIndex } from "@/features/mapgallery/api/mapGalleryApi";
import SelectCampaignMap from "./SelectCampaignMap";

export interface OpenCampaignMapDialogProps {
  show: boolean;
  onClose: () => void;

  /**
   * Used by the parent to show a toast message.
   * (Keeping your existing pattern.)
   */
  onSaveSuccess: (message?: string) => void;

  /**
   * Optional callback so the parent can actually "open/load" the map.
   * If you don't need it yet, you can omit it.
   */
  onOpenMap?: (index: CampaignMapGalleryIndex) => void;
}

export default function OpenCampaignMapDialog({ show, onClose, onSaveSuccess, onOpenMap }: OpenCampaignMapDialogProps) {
  const [filter, setFilter] = useState("");
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [selectedMap, setSelectedMap] = useState<CampaignMapGalleryIndex | null>(null);
  const [reloadNonce, setReloadNonce] = useState(0);

  // Hidden file input used to pick the zip for import.
  const fileInputRef = useRef<HTMLInputElement>(null);
  // Name of the file currently being imported, shown in the import progress dialog.
  const [importFileName, setImportFileName] = useState("");

  const { exportPhase, progressPercent, startExport, resetExport, isActive } = useExportCampaignMap();
  const { importPhase, progressPercent: importProgressPercent, startImport, resetImport, isActive: isImportActive } = useImportCampaignMap();

  // Reset selection/filter each time the dialog opens; cancel any in-flight export/import when it closes.
  useEffect(() => {
    if (!show) {
      resetExport();
      resetImport();
      return;
    }
    setFilter("");
    setSelectedId(null);
    setSelectedMap(null);
  }, [show]);

  // Once an import finishes, refresh the list so the new map appears, then return to idle.
  useEffect(() => {
    if (importPhase.phase === "done") {
      setReloadNonce((value) => value + 1);
    }
  }, [importPhase.phase]);

  const handleOpen = () => {
    if (!selectedMap) return;

    onOpenMap?.(selectedMap);
    onSaveSuccess(`Opened "${selectedMap.mapName}" (${selectedMap.width}×${selectedMap.height}).`);
    onClose();
  };

  const handleExport = () => {
    if (!selectedMap) return;
    startExport(selectedMap.campaignMapGalleryIndexId);
  };

  // Import opens the hidden file picker; selecting a zip kicks off the import.
  const handleImportClick = () => {
    fileInputRef.current?.click();
  };

  const handleFileSelected = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    // Clear the input value so picking the same file again still fires a change event.
    e.target.value = "";
    if (!file) return;

    setImportFileName(file.name);
    startImport(file);
  };

  const handleRowDoubleClick = (item: CampaignMapGalleryIndex) => {
    setSelectedId(item.campaignMapGalleryIndexId);
    setSelectedMap(item);
    onOpenMap?.(item);
    onSaveSuccess(`Opened "${item.mapName}" (${item.width}×${item.height}).`);
    onClose();
  };

  return (
    <>
      <Modal
        open={show}
        onClose={onClose}
        title="Open Map"
        size="lg"
        footer={
          <>
            {/* Export/Import sit on the left, matching the BiomeManagerDialog pattern. */}
            <div className="mr-auto flex items-center gap-2">
              <Button variant="primary" onClick={handleExport} disabled={!selectedMap || isActive || isImportActive}>
                <CiExport aria-hidden="true" strokeWidth={2} />
                Export
              </Button>
              <Button variant="primary" onClick={handleImportClick} disabled={isActive || isImportActive}>
                <CiImport aria-hidden="true" strokeWidth={2} />
                Import
              </Button>
            </div>
            <Button variant="secondary" onClick={onClose}>
              Cancel
            </Button>
            <Button variant="success" onClick={handleOpen} disabled={!selectedMap}>
              Open
            </Button>
          </>
        }>
        <SelectCampaignMap
          enabled={show}
          filter={filter}
          onFilterChange={setFilter}
          selectedMapId={selectedId}
          onSelectedMapIdChange={setSelectedId}
          onSelectedMapChange={setSelectedMap}
          onConfirmSelection={handleRowDoubleClick}
          refreshNonce={reloadNonce}
          tableMaxHeightClassName="max-h-[420px]"
        />
      </Modal>

      <ExportProgressDialog
        open={isActive}
        mapName={selectedMap?.mapName ?? ""}
        exportPhase={exportPhase}
        progressPercent={progressPercent}
        onClose={resetExport}
      />

      {/* Hidden input that backs the Import button. */}
      <input ref={fileInputRef} type="file" accept=".zip" className="hidden" onChange={handleFileSelected} />

      <ImportProgressDialog
        open={isImportActive}
        fileName={importFileName}
        importPhase={importPhase}
        progressPercent={importProgressPercent}
        onClose={resetImport}
      />
    </>
  );
}
