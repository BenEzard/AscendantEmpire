import { useCallback, useEffect, useMemo, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { CiExport } from "react-icons/ci";
import { HiMiniPlus, HiTrash } from "react-icons/hi2";
import { Button, ConfirmDialog, ExportDialog, PagedDialog, Toast, ToastViewport, Tooltip } from "@/components/ui";
import type { Biome } from "@/types/map/Biome";
import type { Geography } from "@/types/map/Geography";
import type { MapResourceCategory, MapResourceType } from "@/types/map/MapResource";
import { safeStringify } from "@/util/json";
import { apiErrorMessages } from "@/util/apiErrors";
import {
  mapResourceCatalogueKey,
  useDeleteMapResourceMutation,
  useMapResourceCatalogueQuery,
  useAssetsQuery,
  useSaveMapResourceCatalogueMutation,
} from "../hooks/useMapResourceCatalogue";
import { createMapResourceType, grows, holdsSingleQuantity, kindOf, toDraft, toPayload, type MapResourceDraft } from "../tools/mapResourceDraft";
import MapResourceGeneralSection from "./mapResources/MapResourceGeneralSection";
import MapResourceYieldsSection from "./mapResources/MapResourceYieldsSection";
import MapResourceHabitatsSection from "./mapResources/MapResourceHabitatsSection";
import MapResourceRenewableSection from "./mapResources/MapResourceRenewableSection";
import MapResourceScenariosSection from "./mapResources/MapResourceScenariosSection";
import MapResourceLayersSection from "./mapResources/MapResourceLayersSection";
import MapResourceCategoriesSection from "./mapResources/MapResourceCategoriesSection";

type SectionKey = "general" | "yields" | "habitats" | "renewable" | "scenarios" | "layers" | "categories";

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
  const assetsQuery = useAssetsQuery();
  const assets = useMemo(() => assetsQuery.data ?? [], [assetsQuery.data]);

  const [draft, setDraft] = useState<MapResourceDraft>({ categories: [], types: [] });
  const [selectedIndex, setSelectedIndex] = useState<number | null>(null);
  const [activeSection, setActiveSection] = useState<SectionKey>("general");
  const [pendingDelete, setPendingDelete] = useState<PendingDelete | null>(null);

  const [errorItems, setErrorItems] = useState<string[]>([]);
  const [showError, setShowError] = useState(false);
  const [showExport, setShowExport] = useState(false);
  const [showDiscardConfirm, setShowDiscardConfirm] = useState(false);

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

  /**
   * Replaces the draft with `source`, keeping the same type selected by code where that type still exists — by code rather than by index,
   * since the index of a given type shifts as rows come and go.
   */
  const reseedFrom = (source: MapResourceCategory[] | undefined) => {
    const fresh = toDraft(source);
    const selectedCode = selectedType?.code;

    setDraft(fresh);
    const index = fresh.types.findIndex((t) => t.code.toLowerCase() === selectedCode?.toLowerCase());
    setSelectedIndex(index >= 0 ? index : fresh.types.length > 0 ? 0 : null);
  };

  /**
   * Reseeds from the catalogue as just refetched. Read straight out of the query cache rather than from `catalogueQuery.data`, which is a render
   * behind immediately after a save.
   */
  const reseedFromServer = () => reseedFrom(queryClient.getQueryData<MapResourceCategory[]>(mapResourceCatalogueKey));

  const saveMutation = useSaveMapResourceCatalogueMutation({
    onSuccess: () => {
      setShowError(false);
      // New types and categories now have ids, so their codes lock and they can be deleted.
      reseedFromServer();
      onSaveSuccess("Map Resources saved successfully.");
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

  /**
   * The unsaved new type, if the designer has one on the go. Identified by its missing id, which the server fills in on save, so no extra state
   * has to be kept in step with the draft.
   *
   * Only ever one at a time, since Add is locked while this is set.
   */
  const inFlightNewType = useMemo(() => draft.types.find((type) => type.id == null) ?? null, [draft.types]);

  /**
   * Add is locked while a new type is unsaved, so a second click cannot push the half-filled one out of reach: the picker sorts by category and
   * sort order, not by when a row was added, so a fresh blank row can easily land somewhere the designer is not looking.
   *
   * Resolved by Save (which gives it an id), by Cancel, or by deleting it with the trash.
   */
  const addLocked = inFlightNewType != null;
  const addDisabled = draft.categories.length === 0 || addLocked;

  // Both sides of the dirty check go through the same toDraft → toPayload path, so an untouched draft serialises byte-for-byte identically to the
  // saved catalogue and the comparison needs no field-by-field diffing. The draft's own payload is what Export writes out, so it is shared.
  //
  // Comparing payloads rather than drafts means a type's `id` and `sortOrder` sit outside the comparison, since toPayload drops both. Neither is
  // editable here — a type's sortOrder is only ever set when the type is created, which shows up as a whole added entry — so nothing a designer
  // can do goes unnoticed. Putting a sortOrder editor on the General page would be the thing that breaks that, and would want comparing drafts.
  const draftPayloadJson = useMemo(() => safeStringify(toPayload(draft)), [draft]);
  const savedPayloadJson = useMemo(() => safeStringify(toPayload(toDraft(catalogueQuery.data))), [catalogueQuery.data]);

  /**
   * True when the draft holds edits the server has not got. Drives whether Cancel is offered at all, and whether it stops to confirm.
   *
   * Compared against the live query data, so a background refetch that genuinely moves the catalogue underneath an open dialog counts as a
   * difference — which it is.
   */
  const isDirty = catalogueLoaded && draftPayloadJson !== savedPayloadJson;

  const busy = saveMutation.isPending || deleteMutation.isPending;

  /** Throws away every unsaved edit and leaves the dialog open. Confirmed first, since nothing short of re-typing brings the edits back. */
  const discardEdits = () => {
    reseedFrom(catalogueQuery.data);
    setShowDiscardConfirm(false);
    setShowError(false);
  };

  // A clean draft has nothing to discard, so the button is disabled and this never fires; the guard is here so a stray call cannot pop an
  // empty-handed confirmation.
  const handleCancelEdits = () => {
    if (!isDirty) return;
    setShowDiscardConfirm(true);
  };

  const noTypeSelected = <p className="text-sm text-slate-500">Select a Map Resource, or add one.</p>;

  // Shown in place of the Renewable page for a kind that never grows, so the page says why it is empty rather than offering fields that the
  // chosen kind has to leave unset.
  const notRenewable = (
    <p className="text-sm text-slate-500">
      {selectedType ? "Only a Renewable or Distribution Map Resource grows, and so survives being exhausted." : "Select a Map Resource, or add one."}
    </p>
  );

  return (
    <>
      {/* `height` pins the page region so the dialog is the same size whatever page is open — and, the panel being centred, stays put on screen
          rather than growing and shifting as you move between pages or as a page's list gains rows. Pages taller than it scroll internally.
          The Component Setup dialog pins itself the same way for the same reason. */}
      <PagedDialog<SectionKey>
        open={show}
        title="Map Resource Type Manager"
        size="xl"
        height={500}
        panelClassName="thick-border-modal"
        onCancel={onClose}
        onCommit={handleSave}
        activeSection={activeSection}
        onSectionChange={setActiveSection}
        header={
          <div className="mb-4 flex flex-wrap items-center gap-2">
            <label htmlFor="map-resource-select" className="text-sm font-semibold whitespace-nowrap">
              Select Map Resource
            </label>
            <select
              id="map-resource-select"
              className="w-64 max-w-full rounded-md border border-slate-300 bg-white px-2 py-2 text-sm"
              value={selectedIndex ?? ""}
              disabled={draft.types.length === 0}
              onChange={(e) => setSelectedIndex(e.target.value === "" ? null : Number(e.target.value))}>
              {draft.types.length === 0 ? <option value="">No Map Resources yet</option> : null}
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
            {/* Plain icon-only controls (no border/padding), matching the add/remove icons in the pages' EditableTables */}
            {/* The tooltip says why Add is locked, since a greyed icon on its own leaves the designer guessing. */}
            <Tooltip
              content={
                addLocked
                  ? `Save or cancel "${inFlightNewType?.displayName || "the new Map Resource"}" before adding another`
                  : draft.categories.length === 0
                    ? "Add a Category before adding a Map Resource"
                    : "Add Map Resource"
              }
              // The cursor lives on the wrapper, not the button: a disabled button swallows its own pointer events, so `disabled:pointer-events-none`
              // below is what lets the hover reach Tooltip's span and show the reason at all — and that also rules out a cursor on the button itself.
              className={addDisabled ? "cursor-not-allowed" : undefined}>
              <button
                type="button"
                className="ml-1 text-[var(--bs-primary)] hover:opacity-70 focus:outline-none disabled:pointer-events-none disabled:opacity-40"
                aria-label="Add Map Resource"
                disabled={addDisabled}
                onClick={addType}>
                {/* 22 rather than the trash's 18, so the shorter Mini cross still matches the trash's visual footprint. */}
                <HiMiniPlus size={22} aria-hidden />
              </button>
            </Tooltip>
            <Tooltip content="Delete Map Resource">
              <button
                type="button"
                className="text-slate-400 hover:text-[var(--bs-danger)] focus:outline-none disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:text-slate-400"
                aria-label="Delete Map Resource"
                disabled={!selectedType}
                onClick={removeType}>
                <HiTrash size={18} aria-hidden />
              </button>
            </Tooltip>
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
            {/* Outlined, so the two Cancels beside each other read as different weights of the same idea rather than as a pair of equal buttons;
                the tooltip spells out which one keeps the dialog open. Disabled on a clean draft, which also makes it the dialog's
                unsaved-changes indicator. */}
            <Tooltip content={isDirty ? "Discard unsaved changes, and keep this dialog open" : "No unsaved changes to discard"}>
              <Button variant="outline-secondary" disabled={!isDirty || busy} onClick={handleCancelEdits}>
                Cancel
              </Button>
            </Tooltip>
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
            // A Distribution holds its Assets in scenarios, and a stratified type in layers.
            disabled: !kind || !holdsSingleQuantity(kind),
            count: selectedType?.yields.length,
            content: selectedType ? <MapResourceYieldsSection type={selectedType} assets={assets} onChange={updateSelectedType} /> : noTypeSelected,
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
            key: "renewable",
            label: "Renewable",
            // Renewable and Distribution both grow, and both carry IsRenewable, which is what these two values hang off.
            disabled: !kind || !grows(kind),
            // Guarded rather than rendered-and-greyed: switching kind clears both values, and an editable page on a kind that must leave them
            // empty would let the designer put back a pair the server then refuses. The nav button being disabled does not stop the page
            // showing, since PagedDialog keeps the active page on screen.
            content:
              selectedType && kind && grows(kind) ? <MapResourceRenewableSection type={selectedType} onChange={updateSelectedType} /> : notRenewable,
          },
          {
            key: "scenarios",
            label: "Scenarios",
            disabled: kind !== "distribution",
            count: selectedType?.distributionScenarios.length,
            content: selectedType ? (
              <MapResourceScenariosSection type={selectedType} assets={assets} biomes={biomes} onChange={updateSelectedType} />
            ) : (
              noTypeSelected
            ),
          },
          {
            key: "layers",
            label: "Layers",
            disabled: kind !== "stratified",
            count: selectedType?.layers.length,
            content: selectedType ? <MapResourceLayersSection type={selectedType} assets={assets} onChange={updateSelectedType} /> : noTypeSelected,
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
        title={`Delete ${pendingDelete?.kind === "category" ? "Category" : "Map Resource"}`}
        message={`Delete "${pendingDelete?.label}"? This cannot be undone.`}
        confirmLabel="Delete"
        confirmVariant="danger"
        busy={deleteMutation.isPending}
        onConfirm={confirmDelete}
        onCancel={() => setPendingDelete(null)}
      />

      <ConfirmDialog
        open={showDiscardConfirm}
        title="Discard unsaved changes"
        message={
          <>
            Every unsaved edit to the Map Resource catalogue will be lost, including any new Map Resource you have not saved yet. This cannot be
            undone.
            {inFlightNewType ? <div className="mt-2">&quot;{inFlightNewType.displayName || "The new Map Resource"}&quot; will be removed.</div> : null}
          </>
        }
        confirmLabel="Discard"
        cancelLabel="Keep editing"
        confirmVariant="danger"
        onConfirm={discardEdits}
        onCancel={() => setShowDiscardConfirm(false)}
      />

      <ToastViewport>
        <Toast
          open={showError}
          onClose={() => setShowError(false)}
          variant="danger"
          autoHideMs={8000}
          message={
            <div>
              <div className="mb-1 font-semibold">Map Resource Error</div>
              <ul className="list-disc pl-5">
                {errorItems.map((message, i) => (
                  <li key={i}>{message}</li>
                ))}
              </ul>
            </div>
          }
        />
      </ToastViewport>

      <ExportDialog show={showExport} title="Export Map Resources" textBody={draftPayloadJson} onClose={() => setShowExport(false)} />
    </>
  );
}
