import { useCallback, useEffect, useMemo, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { CiExport } from "react-icons/ci";
import { Button, ConfirmDialog, ExportDialog, PagedDialog, Toast, ToastViewport } from "@/components/ui";
import type { Biome } from "@/types/map/Biome";
import type { Geography } from "@/types/map/Geography";
import type { MapResourceCategory, MapResourceType } from "@/types/map/MapResource";
import { safeStringify } from "@/util/json";
import { apiErrorMessages } from "@/util/apiErrors";
import {
  mapResourceCatalogueKey,
  useDeleteMapResourceMutation,
  useMapResourceCatalogueQuery,
  useResourcesQuery,
  useSaveMapResourceCatalogueMutation,
} from "../hooks/useMapResourceCatalogue";
import { createMapResourceType, holdsSingleQuantity, kindOf, toDraft, toPayload, type MapResourceDraft } from "../tools/mapResourceDraft";
import MapResourceGeneralSection from "./mapResources/MapResourceGeneralSection";
import MapResourceYieldsSection from "./mapResources/MapResourceYieldsSection";
import MapResourceHabitatsSection from "./mapResources/MapResourceHabitatsSection";
import MapResourceScenariosSection from "./mapResources/MapResourceScenariosSection";
import MapResourceLayersSection from "./mapResources/MapResourceLayersSection";
import MapResourceCategoriesSection from "./mapResources/MapResourceCategoriesSection";

type SectionKey = "general" | "yields" | "habitats" | "scenarios" | "layers" | "categories";

/** A saved row the designer asked to delete, awaiting confirmation. */
type PendingDelete = { kind: "type" | "category"; code: string; label: string };

interface MapResourceTypeManagerDialogProps {
  show: boolean;
  biomes: Biome[];
  geographies: Geography[];
  onClose: () => void;
  onSaveSuccess: (message?: string) => void;
}

/**
 * The MapResource Type Manager: edits the catalogue of MapResourceTypes (Berry, Deer, Coal Seam…) and the categories the map editor groups them
 * under. See docs/design/campaign-map/map-resources.md.
 *
 * Works on a draft of the whole catalogue, taken when the dialog opens and saved in one request. Deleting a saved type or category is the one
 * immediate action (confirmed first): the server refuses it if a map or game still uses the row, and only that row leaves the draft, so other
 * unsaved edits survive.
 */
export default function MapResourceTypeManagerDialog({ show, biomes, geographies, onClose, onSaveSuccess }: MapResourceTypeManagerDialogProps) {
  const queryClient = useQueryClient();
  const catalogueQuery = useMapResourceCatalogueQuery();
  const resourcesQuery = useResourcesQuery();
  const resources = useMemo(() => resourcesQuery.data ?? [], [resourcesQuery.data]);

  const [draft, setDraft] = useState<MapResourceDraft>({ categories: [], types: [] });
  const [selectedIndex, setSelectedIndex] = useState<number | null>(null);
  const [activeSection, setActiveSection] = useState<SectionKey>("general");
  const [pendingDelete, setPendingDelete] = useState<PendingDelete | null>(null);

  const [errorItems, setErrorItems] = useState<string[]>([]);
  const [showError, setShowError] = useState(false);
  const [showExport, setShowExport] = useState(false);

  // Re-seeded only when the dialog opens (and after a save), never on a background refetch, which would throw away unsaved edits.
  const catalogueLoaded = catalogueQuery.data !== undefined;
  useEffect(() => {
    if (!show || !catalogueLoaded) return;

    const fresh = toDraft(catalogueQuery.data);
    setDraft(fresh);
    setSelectedIndex(fresh.types.length > 0 ? 0 : null);
    setActiveSection(fresh.types.length > 0 ? "general" : "categories");
    // eslint-disable-next-line react-hooks/exhaustive-deps -- deliberately only on open / first load, see above.
  }, [show, catalogueLoaded]);

  const selectedType = selectedIndex != null ? (draft.types[selectedIndex] ?? null) : null;
  const kind = selectedType ? kindOf(selectedType) : null;

  const showErrors = (error: unknown) => {
    setErrorItems(apiErrorMessages(error));
    setShowError(true);
  };

  /** Replaces the draft with the catalogue as just refetched, keeping the same type selected by code. */
  const reseedFromServer = () => {
    const fresh = toDraft(queryClient.getQueryData<MapResourceCategory[]>(mapResourceCatalogueKey));
    const selectedCode = selectedType?.code;

    setDraft(fresh);
    const index = fresh.types.findIndex((t) => t.code.toLowerCase() === selectedCode?.toLowerCase());
    setSelectedIndex(index >= 0 ? index : fresh.types.length > 0 ? 0 : null);
  };

  const saveMutation = useSaveMapResourceCatalogueMutation({
    onSuccess: () => {
      setShowError(false);
      // New types and categories now have ids, so their codes lock and they can be deleted.
      reseedFromServer();
      onSaveSuccess("MapResources saved successfully.");
    },
    onError: showErrors,
  });

  const deleteMutation = useDeleteMapResourceMutation({
    onError: (error) => {
      setPendingDelete(null);
      showErrors(error);
    },
  });

  const handleSave = useCallback(() => saveMutation.mutate(toPayload(draft)), [saveMutation, draft]);

  const updateSelectedType = (updated: MapResourceType) => {
    if (selectedIndex == null) return;
    setDraft((prev) => ({ ...prev, types: prev.types.map((t, i) => (i === selectedIndex ? updated : t)) }));
  };

  const addType = () => {
    const categoryCode = draft.categories[0]?.code;
    if (!categoryCode) return;

    const sortOrder = draft.types.filter((t) => t.mapResourceCategoryCode === categoryCode).length * 10;
    setDraft((prev) => ({ ...prev, types: [...prev.types, createMapResourceType(categoryCode, sortOrder)] }));
    setSelectedIndex(draft.types.length);
    setActiveSection("general");
  };

  /** Removes an unsaved row from the draft at once; a saved one waits for confirmation and the server. */
  const removeType = () => {
    if (!selectedType || selectedIndex == null) return;

    if (selectedType.id == null) {
      removeTypeFromDraft(selectedIndex);
      return;
    }

    setPendingDelete({ kind: "type", code: selectedType.code, label: selectedType.displayName });
  };

  const removeTypeFromDraft = (index: number) => {
    const remaining = draft.types.filter((_, i) => i !== index);
    setDraft((prev) => ({ ...prev, types: remaining }));
    setSelectedIndex(remaining.length === 0 ? null : Math.min(index, remaining.length - 1));
  };

  const removeCategory = (category: MapResourceDraft["categories"][number]) => {
    if (category.id == null) {
      setDraft((prev) => ({ ...prev, categories: prev.categories.filter((c) => c !== category) }));
      return;
    }

    setPendingDelete({ kind: "category", code: category.code, label: category.displayName });
  };

  const confirmDelete = () => {
    if (!pendingDelete) return;
    const { kind: deleteKind, code } = pendingDelete;

    deleteMutation.mutate(
      { kind: deleteKind, code },
      {
        onSuccess: () => {
          setPendingDelete(null);

          if (deleteKind === "type") {
            const index = draft.types.findIndex((t) => t.id != null && t.code === code);
            if (index >= 0) removeTypeFromDraft(index);
          } else {
            setDraft((prev) => ({ ...prev, categories: prev.categories.filter((c) => c.code !== code) }));
          }
        },
      },
    );
  };

  // The header's picker, grouped by category in the same order the map editor lists them.
  const groupedTypes = useMemo(
    () =>
      draft.categories
        .slice()
        .sort((a, b) => a.sortOrder - b.sortOrder || a.displayName.localeCompare(b.displayName))
        .map((category) => ({
          category,
          types: draft.types
            .map((type, index) => ({ type, index }))
            .filter(({ type }) => type.mapResourceCategoryCode.toLowerCase() === category.code.toLowerCase())
            .sort((a, b) => a.type.sortOrder - b.type.sortOrder || a.type.displayName.localeCompare(b.type.displayName)),
        })),
    [draft],
  );

  const typeCounts = useMemo(() => {
    const counts: Record<string, number> = {};
    for (const type of draft.types) {
      const key = type.mapResourceCategoryCode.toLowerCase();
      counts[key] = (counts[key] ?? 0) + 1;
    }
    return counts;
  }, [draft.types]);

  const noTypeSelected = <p className="text-sm text-slate-500">Select a MapResource, or add one.</p>;

  return (
    <>
      <PagedDialog<SectionKey>
        open={show}
        title="MapResource Type Manager"
        size="xl"
        panelClassName="thick-border-modal"
        onCancel={onClose}
        onCommit={handleSave}
        activeSection={activeSection}
        onSectionChange={setActiveSection}
        header={
          <div className="mb-4 flex flex-wrap items-center gap-2">
            <label htmlFor="map-resource-select" className="text-sm font-semibold whitespace-nowrap">
              Select MapResource
            </label>
            <select
              id="map-resource-select"
              className="w-64 max-w-full rounded-md border border-slate-300 bg-white px-2 py-2 text-sm"
              value={selectedIndex ?? ""}
              disabled={draft.types.length === 0}
              onChange={(e) => setSelectedIndex(e.target.value === "" ? null : Number(e.target.value))}>
              {draft.types.length === 0 ? <option value="">No MapResources yet</option> : null}
              {groupedTypes.map(({ category, types }) =>
                types.length === 0 ? null : (
                  <optgroup key={category.code} label={category.displayName}>
                    {types.map(({ type, index }) => (
                      <option key={index} value={index}>
                        {type.displayName}
                      </option>
                    ))}
                  </optgroup>
                ),
              )}
            </select>
            <Button variant="secondary" disabled={draft.categories.length === 0} onClick={addType}>
              Add MapResource
            </Button>
            <Button variant="danger" disabled={!selectedType} onClick={removeType}>
              Delete
            </Button>
          </div>
        }
        footer={
          <>
            <div className="mr-auto">
              <Button variant="primary" onClick={() => setShowExport(true)}>
                <CiExport aria-hidden="true" strokeWidth={2} />
                Export
              </Button>
            </div>
            <Button variant="secondary" onClick={onClose}>
              Cancel / Close
            </Button>
            <Button variant="success" disabled={saveMutation.isPending} onClick={handleSave}>
              {saveMutation.isPending ? "Saving..." : "Save"}
            </Button>
          </>
        }
        sections={[
          {
            key: "general",
            label: "General",
            disabled: !selectedType,
            content: selectedType ? (
              <MapResourceGeneralSection type={selectedType} categories={draft.categories} onChange={updateSelectedType} />
            ) : (
              noTypeSelected
            ),
          },
          {
            key: "yields",
            label: "Yields",
            // A Distribution holds its Resources in scenarios, and a stratified type in layers.
            disabled: !kind || !holdsSingleQuantity(kind),
            count: selectedType?.yields.length,
            content: selectedType ? <MapResourceYieldsSection type={selectedType} resources={resources} onChange={updateSelectedType} /> : noTypeSelected,
          },
          {
            key: "habitats",
            label: "Habitats",
            disabled: !selectedType,
            count: selectedType?.habitats.length,
            content: selectedType ? (
              <MapResourceHabitatsSection type={selectedType} biomes={biomes} geographies={geographies} onChange={updateSelectedType} />
            ) : (
              noTypeSelected
            ),
          },
          {
            key: "scenarios",
            label: "Scenarios",
            disabled: kind !== "distribution",
            count: selectedType?.distributionScenarios.length,
            content: selectedType ? (
              <MapResourceScenariosSection type={selectedType} resources={resources} biomes={biomes} onChange={updateSelectedType} />
            ) : (
              noTypeSelected
            ),
          },
          {
            key: "layers",
            label: "Layers",
            disabled: kind !== "stratified",
            count: selectedType?.layers.length,
            content: selectedType ? <MapResourceLayersSection type={selectedType} resources={resources} onChange={updateSelectedType} /> : noTypeSelected,
          },
          {
            key: "categories",
            label: "Categories",
            count: draft.categories.length,
            content: (
              <MapResourceCategoriesSection
                categories={draft.categories}
                typeCounts={typeCounts}
                onChange={(categories) => setDraft((prev) => ({ ...prev, categories }))}
                onRemove={removeCategory}
              />
            ),
          },
        ]}
      />

      <ConfirmDialog
        open={pendingDelete != null}
        title={`Delete ${pendingDelete?.kind === "category" ? "Category" : "MapResource"}`}
        message={`Delete "${pendingDelete?.label}"? This cannot be undone.`}
        confirmLabel="Delete"
        confirmVariant="danger"
        busy={deleteMutation.isPending}
        onConfirm={confirmDelete}
        onCancel={() => setPendingDelete(null)}
      />

      <ToastViewport>
        <Toast
          open={showError}
          onClose={() => setShowError(false)}
          variant="danger"
          autoHideMs={8000}
          message={
            <div>
              <div className="mb-1 font-semibold">MapResource Error</div>
              <ul className="list-disc pl-5">
                {errorItems.map((message, i) => (
                  <li key={i}>{message}</li>
                ))}
              </ul>
            </div>
          }
        />
      </ToastViewport>

      <ExportDialog show={showExport} title="Export MapResources" textBody={safeStringify(toPayload(draft))} onClose={() => setShowExport(false)} />
    </>
  );
}
