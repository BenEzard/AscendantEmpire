import { Button, ExportDialog, PagedDialog, Toast, ToastViewport } from "@/components/ui";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import axios from "axios";
import { CiExport } from "react-icons/ci";
import type { Biome } from "@/types/map/Biome";
import { safeStringify } from "@/util/json";
import { useCampaignMapBiomesQuery, useSaveCampaignMapBiomesMutation } from "@/hooks/useCampaignMapBiomes";
import { isValidColor, pickTextColorOnBg } from "@/util/color";
import BiomeResource from "./BiomeResource";
import { fromRegenerationPercentInput, toRegenerationPercentInput } from "@/util/regenerationPercent";
import type { BiomeType } from "@/types/map/Biome";

interface BiomeManagerProps {
  open: boolean;
  onCancel: () => void;
  onSaveSuccess: (message: string) => void;
}

/**
 * The biome catalogue editor: one page of general properties and two of resource yields, over whichever biome the header
 * dropdown has selected. Self-contained like the other setup dialogs on this tool - it loads the catalogue itself and holds
 * its own editable draft, so nothing reaches the server until Save.
 */
function BiomeManagerDialog({ open, onCancel, onSaveSuccess }: BiomeManagerProps) {
  const { data: biomes } = useCampaignMapBiomesQuery();

  // The editable copy of the catalogue. Every edit lands here; only Save sends it to the server.
  const [biomeDraft, setBiomeDraft] = useState<Biome[]>([]);
  const [selectedBiomeType, setSelectedBiomeType] = useState<BiomeType | null>(null);

  const selectedBiome = biomeDraft.find((b) => b.biomeType === selectedBiomeType) ?? null;

  const [textEditorTitle, setTextEditorTitle] = useState("Text");
  const [textEditorBody, setTextEditorBody] = useState("");
  const [showTextEditor, setShowTextEditor] = useState(false);

  const [showErrorToast, setShowErrorToast] = useState(false);
  const [errorToastItems, setErrorToastItems] = useState<string[]>([]);
  const toggleErrorToast = () => setShowErrorToast(!showErrorToast);
  const [isBiomeDropdownOpen, setIsBiomeDropdownOpen] = useState(false);
  const biomeDropdownRef = useRef<HTMLDivElement | null>(null);

  const mustBeTraversablePaintable = selectedBiome?.isTraversable === false || selectedBiome?.isPaintableOnCampaignMap === false;
  const mustBeTraversablePaintableLand = mustBeTraversablePaintable || selectedBiome?.isWater;
  const isFishDisabled =
    !selectedBiome || selectedBiome.isTraversable === false || selectedBiome.isPaintableOnCampaignMap === false || selectedBiome.isWater === false;
  const selectedBiomeHtmlColor = selectedBiome?.mapEditorHtmlColor;
  const selectedBiomeSwatchColor = isValidColor(selectedBiomeHtmlColor) ? selectedBiomeHtmlColor : "#ffffff";

  const orderedBiomes = useMemo(() => biomeDraft.slice().sort((a, b) => a.name.localeCompare(b.name)), [biomeDraft]);

  // Seeds the draft from the catalogue when the dialog opens, and again once the query lands if it was still in
  // flight at that moment. Guarded by the ref so a later background refetch can never clobber edits in progress.
  const hasSeededDraftRef = useRef(false);

  useEffect(() => {
    if (!open) {
      hasSeededDraftRef.current = false;
      return;
    }

    if (hasSeededDraftRef.current || biomes == null) {
      return;
    }

    const firstByName = biomes.slice().sort((a, b) => a.name.localeCompare(b.name))[0];

    setBiomeDraft(biomes);
    setSelectedBiomeType(firstByName?.biomeType ?? null);
    hasSeededDraftRef.current = true;
  }, [open, biomes]);

  useEffect(() => {
    if (!isBiomeDropdownOpen) return;

    const onPointerDown = (e: PointerEvent) => {
      const root = biomeDropdownRef.current;
      if (!root) return;
      if (e.target instanceof Node && root.contains(e.target)) return;
      setIsBiomeDropdownOpen(false);
    };

    const onEscape = (e: KeyboardEvent) => {
      if (e.key !== "Escape") return;
      setIsBiomeDropdownOpen(false);
    };

    document.addEventListener("pointerdown", onPointerDown, { capture: true });
    document.addEventListener("keydown", onEscape);
    return () => {
      document.removeEventListener("pointerdown", onPointerDown, { capture: true } as AddEventListenerOptions);
      document.removeEventListener("keydown", onEscape);
    };
  }, [isBiomeDropdownOpen]);

  const handleBiomeFieldChange = <K extends keyof Biome>(field: K, value: Biome[K]) => {
    if (!selectedBiome) return;

    const originalId = selectedBiome.biomeType;
    const updated = { ...selectedBiome, [field]: value };

    setBiomeDraft((prev) => prev.map((b) => (b.biomeType === originalId ? updated : b)));
  };

  const handleExportClick = () => {
    setTextEditorTitle("Export Biomes");
    setTextEditorBody(safeStringify(biomeDraft));
    setShowTextEditor(true);
  };

  const saveBiomesMutation = useSaveCampaignMapBiomesMutation({
    onSuccess: () => {
      setShowErrorToast(false);

      onSaveSuccess("Biomes saved successfully.");
    },
    onError: (error: unknown) => {
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
      } else if (error instanceof Error) {
        items = [error.message];
      }

      setErrorToastItems(items);
      setShowErrorToast(true);
      console.error("Save error:", error);
    },
  });

  const handleSave = useCallback(() => saveBiomesMutation.mutate(biomeDraft), [saveBiomesMutation, biomeDraft]);

  return (
    <>
      <PagedDialog
        open={open}
        title="Biome Manager"
        size="lg"
        panelClassName="thick-border-modal"
        onCancel={onCancel}
        onCommit={handleSave}
        header={
          <div className="flex flex-wrap items-center gap-2 mb-4">
            <label className="text-sm font-semibold whitespace-nowrap">Select Biome</label>
            <div ref={biomeDropdownRef} className="relative w-64 max-w-full">
              <button
                type="button"
                className="flex w-full items-center justify-between rounded-md border border-slate-300 bg-white px-2 py-2 text-sm text-slate-700"
                aria-haspopup="listbox"
                aria-expanded={isBiomeDropdownOpen}
                onClick={() => setIsBiomeDropdownOpen((prev) => !prev)}>
                <span className="flex min-w-0 items-center gap-2">
                  <span
                    className="inline-block h-3.5 w-3.5 shrink-0 rounded-sm border border-slate-400"
                    style={{ backgroundColor: selectedBiomeSwatchColor }}
                    aria-hidden="true"
                  />
                  <span className="truncate">{selectedBiome?.name ?? "Select biome"}</span>
                </span>
                <span className="ml-2 text-xs text-slate-500" aria-hidden="true">
                  ▾
                </span>
              </button>

              {isBiomeDropdownOpen && (
                <div
                  role="listbox"
                  className="absolute left-0 top-full z-50 mt-1 max-h-64 w-full overflow-auto rounded-md border border-slate-300 bg-white py-1 shadow-md">
                  {orderedBiomes.map((b) => {
                    const swatchColor = isValidColor(b.mapEditorHtmlColor) ? b.mapEditorHtmlColor : "#ffffff";
                    const isSelected = selectedBiome?.biomeType === b.biomeType;
                    return (
                      <button
                        key={b.biomeType}
                        type="button"
                        role="option"
                        aria-selected={isSelected}
                        className={[
                          "flex w-full items-center gap-2 px-2 py-1.5 text-left text-sm text-slate-700",
                          isSelected ? "bg-slate-100 font-medium" : "hover:bg-slate-50",
                        ].join(" ")}
                        onClick={() => {
                          setSelectedBiomeType(b.biomeType);
                          setIsBiomeDropdownOpen(false);
                        }}>
                        <span
                          className="inline-block h-3.5 w-3.5 shrink-0 rounded-sm border border-slate-400"
                          style={{ backgroundColor: swatchColor }}
                          aria-hidden="true"
                        />
                        <span className="truncate">{b.name}</span>
                      </button>
                    );
                  })}
                </div>
              )}
            </div>
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
            <Button variant="success" disabled={saveBiomesMutation.isPending} onClick={handleSave}>
              {saveBiomesMutation.isPending ? "Saving..." : "Save"}
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
                    {(() => {
                      const htmlColor = selectedBiome?.mapEditorHtmlColor;
                      return (
                        <input
                          className="w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
                          value={htmlColor ?? ""}
                          disabled={!selectedBiome}
                          onChange={(e) => handleBiomeFieldChange("mapEditorHtmlColor", e.target.value)}
                          style={{
                            backgroundColor: isValidColor(htmlColor) ? htmlColor! : "white",
                            color: pickTextColorOnBg(isValidColor(htmlColor) ? htmlColor! : "white"),
                          }}
                        />
                      );
                    })()}
                  </div>
                </div>
              </div>
            ),
          },
          {
            key: "regen",
            label: "Regenerating Resources",
            content: (
              <div className="w-full max-w-xl">
                {/* Stored as a fraction (0.25) but edited as a percent (25): the share of each sustenance Base that regrows every Round. */}
                <BiomeResource
                  label="Regeneration (% of Base / Round)"
                  biome={selectedBiome}
                  field="regenerationPercent"
                  disabled={!selectedBiome || mustBeTraversablePaintable}
                  emptyToNull={true}
                  getValue={(biome) => toRegenerationPercentInput(biome.regenerationPercent)}
                  parseValue={fromRegenerationPercentInput}
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
            ),
          },
          {
            key: "passive",
            label: "Passive Resources",
            content: (
              <div className="w-full max-w-xl">
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

export default BiomeManagerDialog;
