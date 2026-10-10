import { Button, ExportDialog, PagedDialog, Toast, ToastViewport } from "@/components/ui";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { CiExport } from "react-icons/ci";
import type { Geography } from "@/types/map/Geography";
import { safeStringify } from "@/util/json";
import { useCampaignMapGeographiesQuery, useSaveCampaignMapGeographiesMutation } from "@/hooks/useCampaignMapGeographies";
import type { CampaignMapGeographyType } from "@/types/map/Geography";

interface GeographyManagerProps {
  open: boolean;
  onCancel: () => void;
  onSaveSuccess: (message: string) => void;
}

/**
 * The geography catalogue editor, over whichever geography the header dropdown has selected. Self-contained like the other
 * setup dialogs on this tool - it loads the catalogue itself and holds its own editable draft, so nothing reaches the server
 * until Save.
 */
function GeographyManagerDialog({ open, onCancel, onSaveSuccess }: GeographyManagerProps) {
  const { data: geographies } = useCampaignMapGeographiesQuery();

  // The editable copy of the catalogue. Every edit lands here; only Save sends it to the server.
  const [geographyDraft, setGeographyDraft] = useState<Geography[]>([]);
  const [selectedGeographyType, setSelectedGeographyType] = useState<CampaignMapGeographyType | null>(null);

  const [textEditorTitle, setTextEditorTitle] = useState("Text");
  const [textEditorBody, setTextEditorBody] = useState<string>();
  const [showTextEditor, setShowTextEditor] = useState(false);

  const [showErrorToast, setShowErrorToast] = useState(false);
  const [errorToastItems, setErrorToastItems] = useState<string[]>([]);
  const toggleErrorToast = () => setShowErrorToast((v) => !v);

  const orderedGeographies = useMemo(() => geographyDraft.slice().sort((a, b) => a.name.localeCompare(b.name)), [geographyDraft]);

  const selectedGeography = useMemo(() => {
    if (selectedGeographyType == null) return null;
    return geographyDraft.find((g) => g.campaignMapGeographyType === selectedGeographyType) ?? null;
  }, [geographyDraft, selectedGeographyType]);

  // Seeds the draft from the catalogue when the dialog opens, and again once the query lands if it was still in
  // flight at that moment. Guarded by the ref so a later background refetch can never clobber edits in progress.
  const hasSeededDraftRef = useRef(false);

  useEffect(() => {
    if (!open) {
      hasSeededDraftRef.current = false;
      return;
    }

    if (hasSeededDraftRef.current || geographies == null) {
      return;
    }

    const firstByName = geographies.slice().sort((a, b) => a.name.localeCompare(b.name))[0];

    setGeographyDraft(geographies);
    setSelectedGeographyType(firstByName?.campaignMapGeographyType ?? null);
    hasSeededDraftRef.current = true;
  }, [open, geographies]);

  const handleExportClick = () => {
    setTextEditorTitle("Export Geographies");
    setTextEditorBody(safeStringify(geographyDraft));
    setShowTextEditor(true);
  };

  const handleGeographyFieldChange = <K extends keyof Geography>(field: K, value: Geography[K]) => {
    if (selectedGeographyType == null) return;

    setGeographyDraft((prev) => prev.map((g) => (g.campaignMapGeographyType === selectedGeographyType ? { ...g, [field]: value } : g)));
  };

  const saveGeographyMutation = useSaveCampaignMapGeographiesMutation({
    onSuccess: () => {
      setShowErrorToast(false);
      onCancel();
      onSaveSuccess("Geographies saved successfully.");
    },
    onError: (error) => {
      const msg = error instanceof Error ? error.message : "Failed to save geographies.";
      setErrorToastItems([msg]);
      setShowErrorToast(true);
    },
  });

  const handleSave = useCallback(() => saveGeographyMutation.mutate(geographyDraft), [saveGeographyMutation, geographyDraft]);

  return (
    <>
      <PagedDialog
        open={open}
        title="Geography Manager"
        size="lg"
        panelClassName="thick-border-modal"
        onCancel={onCancel}
        onCommit={handleSave}
        header={
          <div className="flex flex-wrap items-center gap-2 mb-4">
            <label className="text-sm font-semibold whitespace-nowrap">Select Geography</label>
            <select
              className="w-64 max-w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
              value={selectedGeography?.campaignMapGeographyType ?? ""}
              // The value is a CampaignMapGeographyType member name, not a number: the API serialises enums as their name. Coercing it
              // with Number() gave NaN, so picking a Geography put nothing in state and the select snapped back to the first option.
              onChange={(e) => setSelectedGeographyType(e.target.value === "" ? null : (e.target.value as CampaignMapGeographyType))}
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
            <Button variant="secondary" onClick={onCancel}>
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
