import { Button, PagedDialog, Toast, ToastViewport } from "@/components/ui";
import { useCallback, useMemo, useState } from "react";
import { CiExport } from "react-icons/ci";
import type { Geography } from "../../../types/map/Geography";
import { safeStringify } from "../../../util/json";
import ExportDialog from "../../../components/ExportDialog";
import { useSaveCampaignMapGeographiesMutation } from "../hooks/useCampaignMapGeographies";

interface GeographyManagerProps {
  geographies: Geography[];
  setGeographies: React.Dispatch<React.SetStateAction<Geography[]>>;

  geographyEditorSelection: number | null;
  onSelectGeographyEditorSelection: (id: number | null) => void;

  show: boolean;
  onClose: () => void;
  onSaveSuccess: (message?: string) => void;
}

function GeographyManagerDialog({
  geographies,
  setGeographies,
  geographyEditorSelection,
  onSelectGeographyEditorSelection,
  show,
  onClose,
  onSaveSuccess,
}: GeographyManagerProps) {
  const [textEditorTitle, setTextEditorTitle] = useState("Text");
  const [textEditorBody, setTextEditorBody] = useState<string>();
  const [showTextEditor, setShowTextEditor] = useState(false);

  const [showErrorToast, setShowErrorToast] = useState(false);
  const [errorToastItems, setErrorToastItems] = useState<string[]>([]);
  const toggleErrorToast = () => setShowErrorToast((v) => !v);

  const orderedGeographies = useMemo(() => geographies.slice().sort((a, b) => a.name.localeCompare(b.name)), [geographies]);

  const selectedGeography = useMemo(() => {
    if (geographyEditorSelection == null) return null;
    return geographies.find((g) => g.campaignMapGeographyType === geographyEditorSelection) ?? null;
  }, [geographies, geographyEditorSelection]);

  const handleExportClick = () => {
    setTextEditorTitle("Export Geographies");
    setTextEditorBody(safeStringify(geographies));
    setShowTextEditor(true);
  };

  const handleGeographyFieldChange = <K extends keyof Geography>(field: K, value: Geography[K]) => {
    if (geographyEditorSelection == null) return;

    setGeographies((prev) => prev.map((g) => (g.campaignMapGeographyType === geographyEditorSelection ? { ...g, [field]: value } : g)));
  };

  const saveGeographyMutation = useSaveCampaignMapGeographiesMutation({
    onSuccess: () => {
      setShowErrorToast(false);
      onClose();
      onSaveSuccess("Geographies saved successfully.");
    },
    onError: (error) => {
      const msg = error instanceof Error ? error.message : "Failed to save geographies.";
      setErrorToastItems([msg]);
      setShowErrorToast(true);
    },
  });

  const handleSave = useCallback(() => saveGeographyMutation.mutate(geographies), [saveGeographyMutation, geographies]);

  return (
    <>
      <PagedDialog
        open={show}
        title="Geography Manager"
        size="lg"
        panelClassName="thick-border-modal"
        onCancel={onClose}
        onCommit={handleSave}
        header={
          <div className="flex flex-wrap items-center gap-2 mb-4">
            <label className="text-sm font-semibold whitespace-nowrap">Select Geography</label>
            <select
              className="w-64 max-w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
              value={selectedGeography?.campaignMapGeographyType ?? ""}
              onChange={(e) => {
                const selectedType = Number(e.target.value);
                onSelectGeographyEditorSelection(Number.isFinite(selectedType) ? selectedType : null);
              }}
              disabled={orderedGeographies.length === 0}>
              {orderedGeographies.length === 0 ? (
                <option value="">(no geographies)</option>
              ) : (
                orderedGeographies.map((g) => (
                  <option key={g.campaignMapGeographyType} value={g.campaignMapGeographyType}>
                    {g.name}
                  </option>
                ))
              )}
            </select>
          </div>
        }
        footer={
          <>
            <div className="mr-auto">
              <Button variant="primary" onClick={handleExportClick}>
                <CiExport aria-hidden="true" strokeWidth={2} />
                Export
              </Button>
            </div>
            <Button variant="secondary" onClick={onClose}>
              Cancel / Close
            </Button>
            <Button variant="success" disabled={saveGeographyMutation.isPending} onClick={handleSave}>
              {saveGeographyMutation.isPending ? "Saving..." : "Save"}
            </Button>
          </>
        }
        sections={[
          {
            key: "general",
            label: "General Properties",
            content: (
              <div className="w-full max-w-xl">
                <div className="grid grid-cols-12 items-center gap-2 mb-2">
                  <label className="col-span-5 text-sm text-slate-700">Name</label>
                  <div className="col-span-7">
                    <input
                      className="w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
                      value={selectedGeography?.name ?? ""}
                      disabled={!selectedGeography}
                      onChange={(e) => handleGeographyFieldChange("name", e.target.value)}
                    />
                  </div>
                </div>

                <div className="grid grid-cols-12 items-center gap-2 mb-2">
                  <label className="col-span-5 text-sm text-slate-700">Relative Elevation</label>
                  <div className="col-span-7">
                    <input
                      className="w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
                      type="number"
                      value={selectedGeography?.relativeElevation ?? ""}
                      disabled={!selectedGeography}
                      onChange={(e) => handleGeographyFieldChange("relativeElevation", Number(e.target.value))}
                    />
                  </div>
                </div>

                <div className="grid grid-cols-12 items-center gap-2 mb-2">
                  <label className="col-span-5 text-sm text-slate-700">Movement Cost</label>
                  <div className="col-span-7">
                    <input
                      className="w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
                      type="number"
                      value={selectedGeography?.movementCost ?? ""}
                      disabled={!selectedGeography}
                      onChange={(e) => handleGeographyFieldChange("movementCost", Number(e.target.value))}
                    />
                  </div>
                </div>

                <div className="grid grid-cols-12 items-center gap-2 mb-2">
                  <label className="col-span-5 text-sm text-slate-700">Map Editor Icon</label>
                  <div className="col-span-7">
                    <input
                      className="w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
                      value={selectedGeography?.mapEditorIcon ?? ""}
                      disabled={!selectedGeography}
                      onChange={(e) => handleGeographyFieldChange("mapEditorIcon", e.target.value)}
                    />
                  </div>
                </div>

                <label className="flex items-center gap-2 text-sm text-slate-700">
                  <input
                    type="checkbox"
                    checked={selectedGeography?.isPaintableOnCampaignMap ?? false}
                    disabled={!selectedGeography}
                    onChange={(e) => handleGeographyFieldChange("isPaintableOnCampaignMap", e.target.checked)}
                  />
                  Is Paintable on Campaign Map?
                </label>
              </div>
            ),
          },
        ]}
      />

      <ToastViewport>
        <Toast
          open={showErrorToast}
          onClose={toggleErrorToast}
          variant="danger"
          autoHideMs={5000}
          message={
            <div>
              <div className="font-semibold mb-1">Save Error</div>
              <ul className="list-disc pl-5">
                {errorToastItems.map((msg, i) => (
                  <li key={i}>{msg}</li>
                ))}
              </ul>
            </div>
          }
        />
      </ToastViewport>

      <ExportDialog show={showTextEditor} title={textEditorTitle} textBody={textEditorBody} onClose={() => setShowTextEditor(false)} />
    </>
  );
}

export default GeographyManagerDialog;
