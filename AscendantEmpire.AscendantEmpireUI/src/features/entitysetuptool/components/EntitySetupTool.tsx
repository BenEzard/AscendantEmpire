// Stub for the developer-only "Entity Setup Tool", created alongside the landing-page split.
// This is a standalone dev tool like the Map Editor — gated behind AuthGate and the EntitySetupTool
// permission by its entry point — and is intentionally separate from the game-side `unitdesign`
// feature. The real functionality will be filled in later.
import { useEffect, useMemo, useState } from "react";
import { FaDatabase, FaMountain, FaPlus, FaSitemap, FaTags, FaTree } from "react-icons/fa6";
import { HiArrowPath, HiMagnifyingGlass, HiXMark } from "react-icons/hi2";
import { GiGearHammer } from "react-icons/gi";

import ToolBar from "@/components/ui/toolbar/Toolbar";
import type { ToolBarGroup } from "@/components/ui/toolbar/Toolbar";
import { ExportDialog, HamburgerMenu, ProgressOperationDialog, SearchDrawer, SearchTable, Toast, ToastViewport } from "@/components/ui";
import { useAccountMenu } from "@/features/auth/hooks/useAccountMenu";

import { useDatabaseSeed } from "../hooks/useDatabaseSeed";
import { useSeedTargets } from "../hooks/useSeedTargets";

import { createComponent, getComponent, updateComponent } from "@/features/unitdesign/api/componentsApi";
import { getComponentList, type ComponentListRow } from "@/features/unitdesign/api/catalogueApi";
import { recompileAllTemplates } from "@/features/unitdesign/api/templatesApi";
import { createTag, getTags, updateTag } from "@/features/unitdesign/api/tagsApi";
import { getTagTypes, saveTagTypes } from "@/features/unitdesign/api/tagTypesApi";
import type { Tag } from "@/features/unitdesign/types/Tag";
import type { TagTypeInfo } from "@/features/unitdesign/types/TagType";
import type { UnitComponent } from "@/features/unitdesign/types/UnitComponent";
import BiomeManagerDialog from "./BiomeManagerDialog";
import ComponentSetupDialog from "./ComponentSetupDialog";
import GeographyManagerDialog from "./GeographyManagerDialog";
import TagSetupDialog from "./TagSetupDialog";
import TagTypeSetupDialog from "./TagTypeSetupDialog";
import SeedSelectionDialog from "./SeedSelectionDialog";
import TechnologyTreeViewer from "./technologytree/TechnologyTreeViewer";
import UnitDesigner from "@/features/unitdesign/UnitDesigner";

// Which catalogue the left-hand search drawer is currently browsing (null = closed).
type SearchKind = "components" | "tags" | null;

/** Pulls a user-facing message out of an unknown thrown value, falling back to `fallback`. */
function errorMessage(err: unknown, fallback: string): string {
  return err instanceof Error ? err.message : fallback;
}

export default function EntitySetupTool() {
  // The signed-in username and the Log out item, shared with every other page's hamburger menu.
  const { headerLabel: accountHeader, group: accountGroup } = useAccountMenu();

  // Controls the ComponentSetupDialog opened by the toolbar's "Add" button.
  const [showComponentSetup, setShowComponentSetup] = useState(false);

  // Controls the TagSetupDialog opened by the Tags group's "Add Tag" button.
  const [showTagSetup, setShowTagSetup] = useState(false);

  // Controls the TagTypeSetupDialog opened by the Tags group's "Tag Types" button.
  const [showTagTypeSetup, setShowTagTypeSetup] = useState(false);

  // Controls the BiomeManagerDialog opened by the Map group's "Biomes" button. The dialog loads the biome
  // catalogue itself, so there is nothing else to hold here.
  const [showBiomeManager, setShowBiomeManager] = useState(false);

  // Controls the GeographyManagerDialog opened by the Map group's "Geographies" button, which loads its own
  // catalogue in the same way.
  const [showGeographyManager, setShowGeographyManager] = useState(false);

  // The tag type catalogue, loaded once on mount. Backs both the Tag Type manager (as its editable set)
  // and the Tag dialog's "Tag Type" dropdown (as its options).
  const [tagTypes, setTagTypes] = useState<TagTypeInfo[]>([]);

  // Controls the full-screen Unit Designer overlay opened by the Designer group. Saves here persist a
  // global/common catalogue template (developerMode) rather than a player-owned custom one, since this dev
  // tool never acquires a game/player token.
  const [showDesigner, setShowDesigner] = useState(false);

  // Controls the full-screen Technology Tree overlay opened by the Technology group. Read-only, and read from
  // the catalogue rather than from any game, so it needs no game/player token either.
  const [showTechnologyTree, setShowTechnologyTree] = useState(false);

  // Controls the target-selection dialog opened by the Database group's "Seed" button.
  const [showSeedConfirm, setShowSeedConfirm] = useState(false);

  // The seedable parts of the database and the dependencies between them, fetched once from the API so the
  // dialog never has to hard-code a list the backend owns.
  const seedTargets = useSeedTargets();

  // Drives the database seed background process (initiate -> poll -> done/error). Progress and the terminal
  // state are rendered by the ProgressOperationDialog (active/done) and the copyable ExportDialog (error).
  const seed = useDatabaseSeed();

  // Success/error toast messages for template operations: the Unit Designer's Save button and the
  // toolbar's "Recompile All Templates" refresh.
  const [templateSuccessMessage, setTemplateSuccessMessage] = useState<string | null>(null);
  const [templateErrorMessage, setTemplateErrorMessage] = useState<string | null>(null);

  // True while a bulk template recompilation is in flight, disabling its toolbar button against double-triggers.
  const [recompiling, setRecompiling] = useState(false);

  // When set, the respective dialog opens in edit mode seeded from this item; undefined = blank "Add".
  const [initialComponent, setInitialComponent] = useState<UnitComponent | undefined>(undefined);
  const [initialTag, setInitialTag] = useState<Tag | undefined>(undefined);

  // Holds the error message from the most recent failed component commit, shown as an Alert in the dialog.
  const [componentError, setComponentError] = useState<string | null>(null);

  // Holds the error message from the most recent failed tag commit, shown as an Alert in the dialog.
  const [tagError, setTagError] = useState<string | null>(null);

  // Holds the error message from the most recent failed tag-type commit, shown as an Alert in the dialog.
  const [tagTypeError, setTagTypeError] = useState<string | null>(null);

  // Success toast messages; non-null while the respective toast is visible.
  const [componentSuccessMessage, setComponentSuccessMessage] = useState<string | null>(null);
  const [tagSuccessMessage, setTagSuccessMessage] = useState<string | null>(null);
  const [tagTypeSuccessMessage, setTagTypeSuccessMessage] = useState<string | null>(null);
  const [biomeSuccessMessage, setBiomeSuccessMessage] = useState<string | null>(null);
  const [geographySuccessMessage, setGeographySuccessMessage] = useState<string | null>(null);

  // Load the tag type catalogue once on mount so it's ready for both the Tag Type manager and the Tag
  // dialog's type dropdown. A failure is logged but non-fatal (the dropdown falls back gracefully).
  useEffect(() => {
    getTagTypes()
      .then(setTagTypes)
      .catch((err) => console.error("Failed to load tag types.", err));
  }, []);

  // --- Search drawer state ---------------------------------------------------------------------
  // Which catalogue the drawer is showing, the fetched rows for each, plus shared loading/error flags.
  const [searchKind, setSearchKind] = useState<SearchKind>(null);
  const [searchLoading, setSearchLoading] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);
  const [componentRows, setComponentRows] = useState<ComponentListRow[]>([]);
  const [tagRows, setTagRows] = useState<Tag[]>([]);

  // Persists a newly authored (or edited) component, then closes the dialog on success. A component with no
  // id has never been saved, so it goes to the create endpoint; anything else is an update keyed on its code.
  // (Both replaced the old bulk-upsert call, which was the catalogue *import* path — a single edit shouldn't
  // be expressed as a catalogue import.) On failure the error is surfaced in the dialog via the `error` prop
  // so the user can correct and retry without losing input.
  const handleComponentCommit = async (component: UnitComponent) => {
    try {
      setComponentError(null);
      const isNew = component.id == null;
      const result = isNew ? await createComponent(component) : await updateComponent(component);
      setShowComponentSetup(false);
      setInitialComponent(undefined);
      const verb = isNew ? "created" : "updated";

      // Deleting a slot also deletes whatever saved templates had fitted into it. That's intended, but the
      // dev shouldn't have to find out by opening the designer later — so it's called out in the toast.
      const unfittedNote =
        result.unfittedTemplateNames.length > 0
          ? ` ${result.removedSlotCount} slot(s) removed, which cleared fitted components from: ${result.unfittedTemplateNames.join(", ")}.`
          : "";

      setComponentSuccessMessage(`Component: "${component.displayName}" ${verb} successfully.${unfittedNote}`);
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
        // Description is optional: send a blank value as null so it persists as SQL NULL, not "".
        description: tag.description.trim() === "" ? null : tag.description,
        tagType: tag.tagType,
        movementDomain: tag.movementDomain,
        // Organisation-group fields: the leadership model and, for unled tags, the authored sizing.
        // Null for every other tag type (the dialog clears them when the type changes).
        leadershipModel: tag.leadershipModel,
        unledBaseSize: tag.unledBaseSize,
        unledDefaultSize: tag.unledDefaultSize,
        unledMaxSize: tag.unledMaxSize,
        systemCreated: tag.systemCreated,
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

  // Persists the whole edited tag type set (the backend upserts every row), then closes the dialog on
  // success and refreshes the local catalogue. On failure the error is surfaced in the dialog so the user
  // can correct and retry without losing their edits.
  const handleTagTypeCommit = async (edited: TagTypeInfo[]) => {
    try {
      setTagTypeError(null);
      await saveTagTypes(edited);
      // Re-fetch so the local copy reflects exactly what was persisted (and feeds the Tag dropdown).
      const fresh = await getTagTypes();
      setTagTypes(fresh);
      setShowTagTypeSetup(false);
      setTagTypeSuccessMessage("Tag types saved successfully.");
    } catch (err) {
      const message = errorMessage(err, "Failed to save tag types.");
      setTagTypeError(message);
      console.error("Failed to save tag types.", err);
    }
  };

  // Double-clicking a component row fetches it in full (the catalogue list rows are lossy) and opens the
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

  // "Components" group plus "Tags", "Designer", "Map" and "Database" groups. Each group's Add opens its setup
  // dialog (blank); the search buttons open the left-hand search drawer populated from the catalogue / tag
  // endpoints; the Components group's refresh recompiles every template's stats snapshot after catalogue
  // component edits; the Map group edits the global map catalogues.
  const toolBarGroups = useMemo<ToolBarGroup[]>(
    () => [
      {
        // This page has no menu of its own, so the hamburger exists purely to host the account
        // header and Log out, keeping them where they are on every other page.
        caption: "Menu",
        buttons: [
          {
            key: "entity-setup-menu",
            render: () => (
              <HamburgerMenu title="Entity Setup menu" tooltip="Menu" header={accountHeader} groups={accountGroup ? [accountGroup] : []} />
            ),
          },
        ],
      },
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
            // Open the drawer immediately, then fetch the full catalogue (bodies + components merged).
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
          {
            key: "recompile-templates",
            icon: <HiArrowPath />,
            tooltip: "Recompile All Templates",
            // Component edits don't propagate to the stats snapshots of templates already built with
            // them, so this refresh recompiles every template's snapshot. Disabled while a run is in
            // flight so it can't be double-triggered; per-template failures come back in the result.
            disabled: recompiling,
            onClick: () => {
              setRecompiling(true);
              recompileAllTemplates()
                .then((result) => {
                  if (result.failures.length > 0) {
                    setTemplateErrorMessage(
                      `Recompiled ${result.recompiledCount} template(s); ${result.failures.length} failed. ${result.failures.join(" ")}`,
                    );
                  } else {
                    setTemplateSuccessMessage(`Recompiled ${result.recompiledCount} template(s).`);
                  }
                })
                .catch((err) => {
                  setTemplateErrorMessage(errorMessage(err, "Failed to recompile templates."));
                  console.error("Failed to recompile templates.", err);
                })
                .finally(() => setRecompiling(false));
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
          {
            key: "tag-types",
            icon: <FaTags />,
            tooltip: "Tag Types",
            // Opens the Tag Type manager over the catalogue loaded on mount (and refreshed after each save).
            onClick: () => {
              setTagTypeError(null);
              setShowTagTypeSetup(true);
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
      {
        caption: "Technology",
        buttons: [
          {
            key: "open-technology-tree",
            icon: <FaSitemap />,
            tooltip: "Technology Tree",
            // Opens the catalogue's prerequisite graph full-screen. Read-only — authoring the catalogue is the
            // database seed's job, so there is nothing to save from here.
            onClick: () => setShowTechnologyTree(true),
          },
        ],
      },
      {
        caption: "Map",
        buttons: [
          {
            key: "biomes",
            icon: <FaTree />,
            tooltip: "Biomes",
            // Opens the biome catalogue editor. Biomes are configuration, so they are authored here rather
            // than in the Map Editor, which only reads the catalogue to paint with and to look values up.
            onClick: () => setShowBiomeManager(true),
          },
          {
            key: "geographies",
            icon: <FaMountain />,
            tooltip: "Geographies",
            // The geography catalogue, authored here for the same reason as biomes.
            onClick: () => setShowGeographyManager(true),
          },
        ],
      },
      {
        caption: "Database",
        buttons: [
          {
            key: "seed-database",
            icon: <FaDatabase />,
            tooltip: "Seed Database",
            // Disabled while a seed is in flight so it can't be double-triggered. Opens a confirmation
            // first; the actual seed starts only once the user confirms.
            disabled: seed.isActive,
            onClick: () => setShowSeedConfirm(true),
          },
        ],
      },
    ],
    // recompiling and seed.isActive each drive a button's disabled state, so the groups rebuild when they flip.
    [recompiling, seed.isActive, accountHeader, accountGroup],
  );

  // Map the seed phase onto the two dialogs: the ProgressOperationDialog shows progress (active) and the
  // success alert (done); the copyable ExportDialog shows the full server error text (error).
  const seedProgressStatus: "active" | "done" | null =
    seed.seedPhase.phase === "starting" || seed.seedPhase.phase === "polling"
      ? "active"
      : seed.seedPhase.phase === "done"
        ? "done"
        : null;
  const seedErrorMessage = seed.seedPhase.phase === "error" ? seed.seedPhase.message : null;

  return (
    <div className="flex min-h-screen flex-col bg-slate-900 text-slate-200">
      <ToolBar groups={toolBarGroups} containerClassName="toolbar flex justify-start border-b border-slate-200 bg-stone-100 sticky top-0 z-10" />

      {/* Main content area. The Components search now renders an inline, sortable table here (replacing the
          old left-hand drawer); when no components search is active we fall back to the stub placeholder. */}
      {searchKind === "components" ? (
        <div className="flex flex-1 flex-col gap-3 p-4">
          {/* Header row: title + live row count, plus an X to close the table back to the placeholder. */}
          <div className="flex items-center justify-between">
            <h2 className="text-lg font-semibold">Components ({componentRows.length})</h2>
            <button
              type="button"
              className="rounded-md p-1 text-slate-400 hover:bg-slate-800 hover:text-slate-200 focus:outline-none"
              aria-label="Close components table"
              onClick={() => setSearchKind(null)}>
              <HiXMark size={20} aria-hidden />
            </button>
          </div>

          <SearchTable<ComponentListRow>
            items={componentRows}
            loading={searchLoading}
            error={searchError}
            getKey={(row) => row.code}
            getSearchText={(row) => `${row.displayName} ${row.code} ${row.componentType}`}
            placeholder="Search components…"
            onActivate={handleComponentRowActivate}
            columns={[
              { header: "Display Name", renderCell: (row) => row.displayName, getSortValue: (row) => row.displayName },
              { header: "Code", renderCell: (row) => row.code, getSortValue: (row) => row.code },
              { header: "Component Type", renderCell: (row) => row.componentType, getSortValue: (row) => row.componentType },
            ]}
          />
        </div>
      ) : (
        <div className="flex flex-1 flex-col items-center justify-center gap-2">
          <h1 className="text-2xl font-semibold">Entity Setup Tool</h1>
          <p className="text-sm text-slate-400">Developer tool — stub. Functionality coming soon.</p>
        </div>
      )}

      {/* Left-hand search drawer for Tags. Components now use the inline SearchTable above instead; Tags
          will get the same treatment in a later ticket. Double-clicking a row opens its setup dialog. */}
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
        tagTypes={tagTypes}
      />

      <TagSetupDialog
        open={showTagSetup}
        initialTag={initialTag}
        tagTypes={tagTypes}
        onCancel={() => {
          setShowTagSetup(false);
          setInitialTag(undefined);
          setTagError(null);
        }}
        onCommit={handleTagCommit}
        error={tagError}
      />

      <TagTypeSetupDialog
        open={showTagTypeSetup}
        initialTagTypes={tagTypes}
        onCancel={() => {
          setShowTagTypeSetup(false);
          setTagTypeError(null);
        }}
        onCommit={handleTagTypeCommit}
        error={tagTypeError}
      />

      <BiomeManagerDialog open={showBiomeManager} onCancel={() => setShowBiomeManager(false)} onSaveSuccess={setBiomeSuccessMessage} />

      <GeographyManagerDialog open={showGeographyManager} onCancel={() => setShowGeographyManager(false)} onSaveSuccess={setGeographySuccessMessage} />

      {/* Full-screen Unit Designer overlay. developerMode routes Save to the dev tool's catalogue endpoints
          (POST/PUT /global-unit-templates), which need no game/player token, and reveals the
          developer-only fields in the properties panel — currently the template's map sprite. */}
      {showDesigner && (
        <div className="fixed inset-0 z-40 flex flex-col">
          <UnitDesigner
            mode="edit"
            developerMode
            onClose={() => setShowDesigner(false)}
            onSaveSuccess={setTemplateSuccessMessage}
            onSaveError={setTemplateErrorMessage}
          />
        </div>
      )}

      {/* Full-screen Technology Tree overlay, mounted the same way as the Unit Designer: a graph of a couple of
          hundred boxes across eight columns needs the whole viewport, not a dialog. It self-loads the catalogue. */}
      {showTechnologyTree && (
        <div className="fixed inset-0 z-40 flex flex-col">
          <TechnologyTreeViewer onClose={() => setShowTechnologyTree(false)} />
        </div>
      )}

      {/* Database seed: pick which parts to seed before the (overwriting) seed runs. */}
      <SeedSelectionDialog
        open={showSeedConfirm}
        targets={seedTargets.targets}
        loading={seedTargets.isLoading}
        error={seedTargets.errorMessage}
        onCancel={() => setShowSeedConfirm(false)}
        onCommit={(targets) => {
          setShowSeedConfirm(false);
          seed.startSeed(targets);
        }}
      />

      {/* Seed progress + success. Errors are shown in the copyable ExportDialog below instead. */}
      <ProgressOperationDialog
        open={seedProgressStatus !== null}
        title="Seeding Database"
        operationLabel="Seeding initial values…"
        progressPercent={seed.progressPercent}
        status={seedProgressStatus ?? "active"}
        successMessage="Database seeded successfully."
        onClose={seed.resetSeed}
      />

      {/* Seed failure: full server error text in a dialog the user can copy. */}
      <ExportDialog
        show={seedErrorMessage !== null}
        title="Database Seed — Error"
        textBody={seedErrorMessage ?? undefined}
        onClose={seed.resetSeed}
      />

      <ToastViewport>
        <Toast open={componentSuccessMessage !== null} message={componentSuccessMessage} variant="success" onClose={() => setComponentSuccessMessage(null)} />
        <Toast open={tagSuccessMessage !== null} message={tagSuccessMessage} variant="success" onClose={() => setTagSuccessMessage(null)} />
        <Toast open={tagTypeSuccessMessage !== null} message={tagTypeSuccessMessage} variant="success" onClose={() => setTagTypeSuccessMessage(null)} />
        <Toast open={biomeSuccessMessage !== null} message={biomeSuccessMessage} variant="success" onClose={() => setBiomeSuccessMessage(null)} />
        <Toast open={geographySuccessMessage !== null} message={geographySuccessMessage} variant="success" onClose={() => setGeographySuccessMessage(null)} />
        <Toast open={templateSuccessMessage !== null} message={templateSuccessMessage} variant="success" onClose={() => setTemplateSuccessMessage(null)} />
        <Toast open={templateErrorMessage !== null} message={templateErrorMessage} variant="danger" onClose={() => setTemplateErrorMessage(null)} />
      </ToastViewport>
    </div>
  );
}
