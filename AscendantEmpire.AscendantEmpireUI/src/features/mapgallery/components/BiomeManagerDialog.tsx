import { Button, Modal, Toast, ToastViewport } from "@/components/ui";
import { useMemo, useState } from "react";
import axios from "axios";
import { CiExport } from "react-icons/ci";
import type { Biome } from "../../../types/map/Biome";
import { safeStringify } from "../../../util/json";
import { useSaveCampaignMapBiomesMutation } from "../hooks/useCampaignMapBiomes";
import { isValidColor, pickTextColorOnBg } from "../../../util/color";
import ExportDialog from "../../../components/ExportDialog";
import BiomeResource from "./BiomeResource";

interface BiomeManagerProps {
  biomes: Biome[];
  setBiomes: React.Dispatch<React.SetStateAction<Biome[]>>;

  biomeManagerSelection: number | null;
  onSelectBiomeManagerSelection: (biomeType: number | null) => void;

  show: boolean;
  onClose: () => void;
  onSaveSuccess: (message?: string) => void;
}

function BiomeManagerDialog({
  biomes,
  setBiomes,
  biomeManagerSelection,
  onSelectBiomeManagerSelection,
  show,
  onClose,
  onSaveSuccess,
}: BiomeManagerProps) {
  type SectionKey = "general" | "regen" | "passive";

  const selectedBiome = biomes.find((b) => b.biomeType === biomeManagerSelection) ?? null;
  const [activeSection, setActiveSection] = useState<SectionKey>("general");

  // Text Editor Dialog
  const [textEditorTitle, setTextEditorTitle] = useState("Text");
  const [textEditorBody, setTextEditorBody] = useState("");
  const [showTextEditor, setShowTextEditor] = useState(false);

  const [showErrorToast, setShowErrorToast] = useState(false);
  const [errorToastItems, setErrorToastItems] = useState<string[]>([]);
  const toggleErrorToast = () => setShowErrorToast(!showErrorToast);

  const mustBeTraversablePaintable = selectedBiome?.isTraversable === false || selectedBiome?.isPaintableOnCampaignMap === false;
  const mustBeTraversablePaintableLand = mustBeTraversablePaintable || selectedBiome?.isWater;
  const isFishDisabled =
    !selectedBiome || selectedBiome.isTraversable === false || selectedBiome.isPaintableOnCampaignMap === false || selectedBiome.isWater === false;

  const orderedBiomes = useMemo(() => biomes.slice().sort((a, b) => a.name.localeCompare(b.name)), [biomes]);

  const handleBiomeFieldChange = <K extends keyof Biome>(field: K, value: Biome[K]) => {
    if (!selectedBiome) return;

    const originalId = selectedBiome.biomeType;
    const updated = { ...selectedBiome, [field]: value };

    setBiomes((prev) => prev.map((b) => (b.biomeType === originalId ? updated : b)));
  };

  const handleExportClick = () => {
    setTextEditorTitle("Export Biomes");
    setTextEditorBody(safeStringify(biomes));
    setShowTextEditor(true);
  };

  const saveBiomesMutation = useSaveCampaignMapBiomesMutation({
    onSuccess: () => {
      setShowErrorToast(false);

      onSaveSuccess("Biomes saved successfully.");
    },
    onError: (error: any) => {
      let items: string[] = ["An unknown error occurred."];

      if (axios.isAxiosError(error)) {
        const data = error.response?.data;
        if (Array.isArray(data?.errors)) {
          items = data.errors as string[];
        } else if (typeof data === "string") {
          items = [data];
        } else if (data?.message) {
          items = [data.message as string];
        } else if (error.message) {
          items = [error.message as string];
        }
      } else if (error?.message) {
        items = [error.message as string];
      }

      setErrorToastItems(items);
      setShowErrorToast(true);
      console.error("Save error:", error);
    },
  });

  return (
    <>
      <Modal
        open={show}
        onClose={onClose}
        title="Biome Manager"
        size="lg"
        panelClassName="thick-border-modal"
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
            <Button variant="success" disabled={saveBiomesMutation.isPending} onClick={() => saveBiomesMutation.mutate(biomes)}>
              {saveBiomesMutation.isPending ? "Saving..." : "Save"}
            </Button>
          </>
        }>
        <div className="flex flex-wrap items-center gap-2 mb-4">
          <label className="text-sm font-semibold whitespace-nowrap">Select Biome</label>
          <select
            className="w-64 max-w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
            value={selectedBiome?.biomeType ?? ""}
            onChange={(e) => {
              const raw = e.target.value;
              onSelectBiomeManagerSelection(raw === "" ? null : Number(raw));
            }}>
            {orderedBiomes.map((b) => (
              <option key={b.biomeType} value={b.biomeType}>
                {b.name}
              </option>
            ))}
          </select>
        </div>

        <div className="grid grid-cols-12 gap-4">
          {/* Left: section list */}
          <div className="col-span-12 md:col-span-4">
            <div className="rounded-md border border-slate-200 bg-white">
              <button
                type="button"
                onClick={() => setActiveSection("general")}
                className={[
                  "block w-full px-3 py-2 text-left text-sm",
                  activeSection === "general" ? "nav-item-selected font-semibold" : "hover:bg-slate-50",
                ].join(" ")}>
                General Properties
              </button>
              <button
                type="button"
                onClick={() => setActiveSection("regen")}
                className={[
                  "block w-full px-3 py-2 text-left text-sm border-t border-slate-200",
                  activeSection === "regen" ? "nav-item-selected font-semibold" : "hover:bg-slate-50",
                ].join(" ")}>
                Regenerating Resources
              </button>
              <button
                type="button"
                onClick={() => setActiveSection("passive")}
                className={[
                  "block w-full px-3 py-2 text-left text-sm border-t border-slate-200",
                  activeSection === "passive" ? "nav-item-selected font-semibold" : "hover:bg-slate-50",
                ].join(" ")}>
                Passive Resources
              </button>
            </div>
          </div>

          {/* Right: selected section (single column) */}
          <div className="col-span-12 md:col-span-8">
            <div className="relative w-full max-w-xl">
              <div className={activeSection === "general" ? "" : "invisible pointer-events-none"}>
                <div className="grid grid-cols-12 items-center gap-2 mb-2">
                  <label className="col-span-5 text-sm text-slate-700">Id</label>
                  <div className="col-span-7">
                    <input
                      className="w-full rounded-md border border-slate-300 px-2 py-2 text-sm bg-gray-100"
                      value={selectedBiome?.biomeType ?? ""}
                      disabled
                      readOnly
                    />
                  </div>
                </div>

                <div className="grid grid-cols-12 items-center gap-2 mb-2">
                  <label className="col-span-5 text-sm text-slate-700">Name</label>
                  <div className="col-span-7">
                    <input
                      className="w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
                      value={selectedBiome?.name ?? ""}
                      onChange={(e) => handleBiomeFieldChange("name", e.target.value)}
                      disabled={!selectedBiome}
                    />
                  </div>
                </div>

                <label className="flex items-center gap-2 text-sm text-slate-700 mb-2">
                  <input
                    type="checkbox"
                    checked={selectedBiome?.isTraversable ?? false}
                    onChange={(e) => handleBiomeFieldChange("isTraversable", e.target.checked)}
                    disabled={!selectedBiome}
                  />
                  Is Traversable?
                </label>

                <div className="grid grid-cols-12 items-center gap-2 mb-2">
                  <label className="col-span-5 text-sm text-slate-700">Movement Cost</label>
                  <div className="col-span-7">
                    <input
                      className="w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
                      type="number"
                      value={selectedBiome?.movementCost ?? ""}
                      onChange={(e) => handleBiomeFieldChange("movementCost", Number(e.target.value))}
                      disabled={!selectedBiome}
                    />
                  </div>
                </div>

                <label className="flex items-center gap-2 text-sm text-slate-700 mb-2">
                  <input
                    type="checkbox"
                    checked={selectedBiome?.isWater ?? false}
                    disabled={!selectedBiome || selectedBiome?.canBeElevated}
                    onChange={(e) => handleBiomeFieldChange("isWater", e.target.checked)}
                  />
                  Is Water?
                </label>

                <label className="flex items-center gap-2 text-sm text-slate-700 mb-2">
                  <input
                    type="checkbox"
                    checked={selectedBiome?.doesBlockSight ?? false}
                    disabled={!selectedBiome || selectedBiome?.isWater}
                    onChange={(e) => handleBiomeFieldChange("doesBlockSight", e.target.checked)}
                  />
                  Does Block Sight?
                </label>

                <label className="flex items-center gap-2 text-sm text-slate-700 mb-2">
                  <input
                    type="checkbox"
                    checked={selectedBiome?.canBeElevated ?? false}
                    disabled={!selectedBiome || selectedBiome?.isWater}
                    onChange={(e) => handleBiomeFieldChange("canBeElevated", e.target.checked)}
                  />
                  Can be Elevated?
                </label>

                <label className="flex items-center gap-2 text-sm text-slate-700 mb-2">
                  <input
                    type="checkbox"
                    checked={selectedBiome?.isPaintableOnCampaignMap ?? true}
                    disabled={!selectedBiome}
                    onChange={(e) => handleBiomeFieldChange("isPaintableOnCampaignMap", e.target.checked)}
                  />
                  Is Paintable?
                </label>

                <div className="grid grid-cols-12 items-center gap-2 mb-2">
                  <label className="col-span-5 text-sm text-slate-700">HTML Color</label>
                  <div className="col-span-7">
                    <input
                      className="w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
                      value={selectedBiome?.mapEditorHtmlColor ?? ""}
                      disabled={!selectedBiome}
                      onChange={(e) => handleBiomeFieldChange("mapEditorHtmlColor", e.target.value)}
                      style={{
                        backgroundColor: isValidColor(selectedBiome?.mapEditorHtmlColor) ? selectedBiome?.mapEditorHtmlColor! : "white",
                        color: pickTextColorOnBg(isValidColor(selectedBiome?.mapEditorHtmlColor) ? selectedBiome!.mapEditorHtmlColor! : "white"),
                      }}
                    />
                  </div>
                </div>
              </div>

              {activeSection === "regen" && (
                <div className="absolute inset-0 overflow-auto">
                  <BiomeResource
                    label="Regeneration Rate"
                    biome={selectedBiome}
                    field="regenerationRate"
                    disabled={!selectedBiome || mustBeTraversablePaintable}
                    emptyToNull={true}
                    onChange={handleBiomeFieldChange}
                  />

                  <BiomeResource
                    label="Base Forage"
                    biome={selectedBiome}
                    field="baseForageValue"
                    disabled={!selectedBiome || mustBeTraversablePaintableLand}
                    placeholder="specify..."
                    disabledPlaceholder="n/a"
                    onChange={handleBiomeFieldChange}
                  />

                  <BiomeResource
                    label="Base Hunt"
                    biome={selectedBiome}
                    field="baseHuntValue"
                    disabled={!selectedBiome || mustBeTraversablePaintableLand}
                    placeholder="specify..."
                    disabledPlaceholder="n/a"
                    onChange={handleBiomeFieldChange}
                  />

                  <BiomeResource
                    label="Base Fish"
                    biome={selectedBiome}
                    field="baseFishValue"
                    disabled={isFishDisabled}
                    placeholder="specify..."
                    disabledPlaceholder="n/a"
                    onChange={handleBiomeFieldChange}
                  />

                  <BiomeResource
                    label="Base Fodder"
                    biome={selectedBiome}
                    field="baseFodderValue"
                    disabled={!selectedBiome || mustBeTraversablePaintableLand}
                    placeholder="specify..."
                    disabledPlaceholder="n/a"
                    onChange={handleBiomeFieldChange}
                  />
                </div>
              )}

              {activeSection === "passive" && (
                <div className="absolute inset-0 overflow-auto">
                  <BiomeResource
                    label="Base Bone"
                    biome={selectedBiome}
                    field="baseBoneValue"
                    disabled={!selectedBiome || mustBeTraversablePaintable}
                    placeholder="specify..."
                    disabledPlaceholder="n/a"
                    onChange={handleBiomeFieldChange}
                  />

                  <BiomeResource
                    label="Base Wood"
                    biome={selectedBiome}
                    field="baseWoodValue"
                    disabled={!selectedBiome || mustBeTraversablePaintableLand}
                    placeholder="specify..."
                    disabledPlaceholder="n/a"
                    onChange={handleBiomeFieldChange}
                  />

                  <BiomeResource
                    label="Base Stone"
                    biome={selectedBiome}
                    field="baseStoneValue"
                    disabled={!selectedBiome || mustBeTraversablePaintableLand}
                    placeholder="specify..."
                    disabledPlaceholder="n/a"
                    onChange={handleBiomeFieldChange}
                  />

                  <BiomeResource
                    label="Base Special"
                    biome={selectedBiome}
                    field="baseSpecialValue"
                    disabled={!selectedBiome || mustBeTraversablePaintable}
                    placeholder="specify..."
                    disabledPlaceholder="n/a"
                    onChange={handleBiomeFieldChange}
                  />
                </div>
              )}
            </div>
          </div>
        </div>
      </Modal>

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

export default BiomeManagerDialog;
