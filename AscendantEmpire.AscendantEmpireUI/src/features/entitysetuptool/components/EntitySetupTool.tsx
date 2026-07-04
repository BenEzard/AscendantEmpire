// Stub for the developer-only "Entity Setup Tool", created alongside the landing-page split.
// This is a standalone dev tool like the Map Editor (no AuthGate) and is intentionally separate
// from the game-side `unitdesign` feature. The real functionality will be filled in later.
import { useMemo, useState } from "react";
import { FaPlus } from "react-icons/fa6";
import { HiMagnifyingGlass } from "react-icons/hi2";
import { GiGearHammer } from "react-icons/gi";

import ToolBar from "@/components/Toolbar";
import type { ToolBarGroup } from "@/components/Toolbar";
import { SearchDrawer, Toast, ToastViewport } from "@/components/ui";

import { bulkUpsertComponents, getComponent } from "@/features/unitdesign/api/componentsApi";
import { getComponentList, type ComponentListRow } from "@/features/unitdesign/api/catalogApi";
import { createTag, getTags, updateTag } from "@/features/unitdesign/api/tagsApi";
import type { Tag } from "@/features/unitdesign/types/Tag";
import type { UnitComponent } from "@/features/unitdesign/types/UnitComponent";
import ComponentSetupDialog from "./ComponentSetupDialog";
import TagSetupDialog from "./TagSetupDialog";
import UnitDesigner from "@/features/unitdesign/UnitDesigner";

// Which catalog the left-hand search drawer is currently browsing (null = closed).
type SearchKind = "components" | "tags" | null;

/** Pulls a user-facing message out of an unknown thrown value, falling back to `fallback`. */
function errorMessage(err: unknown, fallback: string): string {
  return err instanceof Error ? err.message : fallback;
}

export default function EntitySetupTool() {
  // Controls the ComponentSetupDialog opened by the toolbar's "Add" button.
  const [showComponentSetup, setShowComponentSetup] = useState(false);

  // Controls the TagSetupDialog opened by the Tags group's "Add Tag" button.
  const [showTagSetup, setShowTagSetup] = useState(false);

  // Controls the full-screen Unit Designer overlay opened by the Designer group. Saves here persist a
  // global/common catalog template (saveAsGlobal) rather than a player-owned custom one, since this dev
  // tool never acquires a game/player token.
  const [showDesigner, setShowDesigner] = useState(false);

  // Success/error toast messages for the Unit Designer's Save button.
  const [templateSuccessMessage, setTemplateSuccessMessage] = useState<string | null>(null);
  const [templateErrorMessage, setTemplateErrorMessage] = useState<string | null>(null);

  // When set, the respective dialog opens in edit mode seeded from this item; undefined = blank "Add".
  const [initialComponent, setInitialComponent] = useState<UnitComponent | undefined>(undefined);
  const [initialTag, setInitialTag] = useState<Tag | undefined>(undefined);

  // Holds the error message from the most recent failed component commit, shown as an Alert in the dialog.
  const [componentError, setComponentError] = useState<string | null>(null);

  // Holds the error message from the most recent failed tag commit, shown as an Alert in the dialog.
  const [tagError, setTagError] = useState<string | null>(null);

  // Success toast messages; non-null while the respective toast is visible.
  const [componentSuccessMessage, setComponentSuccessMessage] = useState<string | null>(null);
  const [tagSuccessMessage, setTagSuccessMessage] = useState<string | null>(null);

  // --- Search drawer state ---------------------------------------------------------------------
  // Which catalog the drawer is showing, the fetched rows for each, plus shared loading/error flags.
  const [searchKind, setSearchKind] = useState<SearchKind>(null);
  const [searchLoading, setSearchLoading] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);
  const [componentRows, setComponentRows] = useState<ComponentListRow[]>([]);
  const [tagRows, setTagRows] = useState<Tag[]>([]);

  // Persists a newly authored (or edited) component via BulkUpsertUnitComponents, then closes the dialog
  // on success. Bulk-upsert keys on Code, so the same call both creates and updates. On failure the error
  // is surfaced in the dialog via the `error` prop so the user can correct and retry without losing input.
  const handleComponentCommit = async (component: UnitComponent) => {
    try {
      setComponentError(null);
      const result = await bulkUpsertComponents(component);
      setShowComponentSetup(false);
      setInitialComponent(undefined);
      const verb = result.addedCount > 0 ? "created" : "updated";
      setComponentSuccessMessage(`Component: "${component.displayName}" ${verb} successfully.`);
    } catch (err) {
      const message = errorMessage(err, "Failed to save component.");
      setComponentError(message);
      console.error("Failed to save component.", err);
    }
  };

  // Persists a tag, then closes the dialog on success. A tag with an existing id is updated; otherwise it
  // is created. On failure the error is surfaced in the dialog via the `error` prop so the user can
  // correct and retry without losing their input.
  const handleTagCommit = async (tag: Tag) => {
    try {
      setTagError(null);
      const payload = {
        code: tag.code,
        displayName: tag.displayName,
        description: tag.description,
        tagType: tag.tagType,
      };

      if (tag.id != null) {
        await updateTag(tag.id, payload);
      } else {
        await createTag(payload);
      }

      setShowTagSetup(false);
      setInitialTag(undefined);
      const verb = tag.id != null ? "updated" : "created";
      setTagSuccessMessage(`Tag: "${tag.displayName}" ${verb} successfully.`);
    } catch (err) {
      const message = errorMessage(err, "Failed to save tag.");
      setTagError(message);
      console.error("Failed to save tag.", err);
    }
  };

  // Double-clicking a component row fetches it in full (the catalog list rows are lossy) and opens the
  // Component Setup dialog seeded with it for editing.
  const handleComponentRowActivate = async (row: ComponentListRow) => {
    try {
      setSearchError(null);
      const component = await getComponent(row.code);
      setInitialComponent(component);
      setComponentError(null);
      setShowComponentSetup(true);
      setSearchKind(null);
    } catch (err) {
      // Keep the drawer open and report the failure there so the user can pick another row.
      setSearchError(errorMessage(err, "Failed to load component."));
      console.error("Failed to load component.", err);
    }
  };

  // Double-clicking a tag row opens the Tag Setup dialog for editing. The row already carries everything
  // the editor needs (id + tagType), so no extra fetch is required.
  const handleTagRowActivate = (tag: Tag) => {
    setInitialTag(tag);
    setTagError(null);
    setShowTagSetup(true);
    setSearchKind(null);
  };

  // "Components" group plus a "Tags" group. Each group's Add opens its setup dialog (blank); the search
  // buttons open the left-hand search drawer populated from the catalog / tag endpoints.
  const toolBarGroups = useMemo<ToolBarGroup[]>(
    () => [
      {
        caption: "Components",
        buttons: [
          {
            key: "add-component",
            icon: <FaPlus />,
            tooltip: "Add Component",
            // Clear any stale error / edit seed from a previous attempt so Add always starts blank.
            onClick: () => {
              setComponentError(null);
              setInitialComponent(undefined);
              setShowComponentSetup(true);
            },
          },
          {
            key: "search-components",
            icon: <HiMagnifyingGlass />,
            tooltip: "Search Components",
            // Open the drawer immediately, then fetch the full catalog (bodies + components merged).
            onClick: () => {
              setSearchKind("components");
              setSearchError(null);
              setSearchLoading(true);
              getComponentList()
                .then(setComponentRows)
                .catch((err) => setSearchError(errorMessage(err, "Failed to load components.")))
                .finally(() => setSearchLoading(false));
            },
          },
        ],
      },
      {
        caption: "Tags",
        buttons: [
          {
            key: "add-tag",
            icon: <FaPlus />,
            tooltip: "Add Tag",
            // Clear any stale error / edit seed from a previous attempt so Add always starts blank.
            onClick: () => {
              setTagError(null);
              setInitialTag(undefined);
              setShowTagSetup(true);
            },
          },
          {
            key: "search-tag",
            icon: <HiMagnifyingGlass />,
            tooltip: "Search Tag",
            // Open the drawer immediately, then fetch every tag (TagView carries the tagType the editor needs).
            onClick: () => {
              setSearchKind("tags");
              setSearchError(null);
              setSearchLoading(true);
              getTags()
                .then(setTagRows)
                .catch((err) => setSearchError(errorMessage(err, "Failed to load tags.")))
                .finally(() => setSearchLoading(false));
            },
          },
        ],
      },
      {
        caption: "Designer",
        buttons: [
          {
            key: "open-designer",
            icon: <GiGearHammer />,
            tooltip: "Open Unit Designer",
            // Opens the shared assembly bench full-screen, in edit mode, self-loading the catalogue.
            onClick: () => setShowDesigner(true),
          },
        ],
      },
    ],
    [],
  );

  return (
    <div className="flex min-h-screen flex-col bg-slate-900 text-slate-200">
      <ToolBar groups={toolBarGroups} containerClassName="toolbar flex justify-start border-b border-slate-200 bg-stone-100 sticky top-0 z-10" />

      <div className="flex flex-1 flex-col items-center justify-center gap-2">
        <h1 className="text-2xl font-semibold">Entity Setup Tool</h1>
        <p className="text-sm text-slate-400">Developer tool — stub. Functionality coming soon.</p>
      </div>

      {/* Left-hand search drawer — one instance per catalog, picked by `searchKind`. Each is generic over
          its row type; double-clicking a row opens it in the matching setup dialog. */}
      {searchKind === "components" && (
        <SearchDrawer<ComponentListRow>
          open
          title="Search Components"
          items={componentRows}
          loading={searchLoading}
          error={searchError}
          getKey={(row) => row.code}
          getPrimaryText={(row) => row.displayName}
          getSecondaryText={(row) => `${row.code} · ${row.componentType}`}
          getSearchText={(row) => `${row.displayName} ${row.code} ${row.componentType}`}
          placeholder="Search components…"
          onClose={() => setSearchKind(null)}
          onActivate={handleComponentRowActivate}
        />
      )}

      {searchKind === "tags" && (
        <SearchDrawer<Tag>
          open
          title="Search Tag"
          items={tagRows}
          loading={searchLoading}
          error={searchError}
          getKey={(tag) => tag.code}
          getPrimaryText={(tag) => tag.displayName}
          getSecondaryText={(tag) => `${tag.code} · ${tag.tagType}`}
          getSearchText={(tag) => `${tag.displayName} ${tag.code} ${tag.tagType}`}
          placeholder="Search tags…"
          onClose={() => setSearchKind(null)}
          onActivate={handleTagRowActivate}
        />
      )}

      <ComponentSetupDialog
        open={showComponentSetup}
        initialComponent={initialComponent}
        onCancel={() => {
          setShowComponentSetup(false);
          setInitialComponent(undefined);
          setComponentError(null);
        }}
        onCommit={handleComponentCommit}
        error={componentError}
      />

      <TagSetupDialog
        open={showTagSetup}
        initialTag={initialTag}
        onCancel={() => {
          setShowTagSetup(false);
          setInitialTag(undefined);
          setTagError(null);
        }}
        onCommit={handleTagCommit}
        error={tagError}
      />

      {/* Full-screen Unit Designer overlay. saveAsGlobal routes Save to the dev tool's catalog endpoints
          (POST/PUT /unit-design/templates/global), which need no game/player token. */}
      {showDesigner && (
        <div className="fixed inset-0 z-40 flex flex-col">
          <UnitDesigner
            mode="edit"
            saveAsGlobal
            onClose={() => setShowDesigner(false)}
            onSaveSuccess={setTemplateSuccessMessage}
            onSaveError={setTemplateErrorMessage}
          />
        </div>
      )}

      <ToastViewport>
        <Toast open={componentSuccessMessage !== null} message={componentSuccessMessage} variant="success" onClose={() => setComponentSuccessMessage(null)} />
        <Toast open={tagSuccessMessage !== null} message={tagSuccessMessage} variant="success" onClose={() => setTagSuccessMessage(null)} />
        <Toast open={templateSuccessMessage !== null} message={templateSuccessMessage} variant="success" onClose={() => setTemplateSuccessMessage(null)} />
        <Toast open={templateErrorMessage !== null} message={templateErrorMessage} variant="danger" onClose={() => setTemplateErrorMessage(null)} />
      </ToastViewport>
    </div>
  );
}
