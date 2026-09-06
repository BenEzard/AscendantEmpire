// Developer-only "System" tool (a standalone dev tool like the Map Editor), gated behind login via
// AuthGate since its user-administration endpoints require an authenticated bearer token. Its first
// feature is user administration: a toolbar "Users" button loads every user into a table where each
// account can be inactivated for a reason (Quit/Banned/Deleted) or reactivated. A "Games" button lists
// every game and offers a confirmed, permanent delete of one.
import { useCallback, useEffect, useMemo, useRef, useState, type ChangeEvent } from "react";
import { HiUsers, HiRectangleStack } from "react-icons/hi2";
import { FaFileExport, FaFileImport } from "react-icons/fa6";

import ToolBar from "@/components/ui/toolbar/Toolbar";
import type { ToolBarGroup } from "@/components/ui/toolbar/Toolbar";
import { Alert, ConfirmDialog, HamburgerMenu, ProgressOperationDialog } from "@/components/ui";
import { hasPermission } from "@/api/authToken";
import { useAccountMenu } from "@/features/auth/hooks/useAccountMenu";

import { getUsers, setUserInactive, setUserPermissions } from "../api/usersApi";
import { getGames } from "../api/gamesApi";
import { INACTIVATION_REASONS } from "../types/User";
import type { AuthUser, InactiveReasonType } from "../types/User";
import type { GameSummary } from "../types/Game";
import type { PermissionType } from "../types/Permission";
import UsersTable from "./UsersTable";
import GamesTable from "./GamesTable";
import UserPermissionsDialog from "./UserPermissionsDialog";
import ExportSelectionDialog from "./ExportSelectionDialog";
import { useCatalogueExport, type CatalogueExportPhase } from "../hooks/useCatalogueExport";
import { useCatalogueImport, type CatalogueImportPhase } from "../hooks/useCatalogueImport";
import { useGameDelete, type GameDeletePhase } from "../hooks/useGameDelete";

// Which panel the page is currently showing. Each toolbar group flips to its own view.
type SystemView = "none" | "users" | "games";

// Short status labels shown under the progress bar for each phase of the export/import state machines.
const EXPORT_PHASE_LABELS: Record<CatalogueExportPhase["phase"], string> = {
  idle: "",
  starting: "Starting export…",
  polling: "Export in progress…",
  downloading: "Preparing download…",
  done: "",
  error: "",
};

const IMPORT_PHASE_LABELS: Record<CatalogueImportPhase["phase"], string> = {
  idle: "",
  starting: "Starting import…",
  polling: "Import in progress…",
  done: "",
  error: "",
};

const DELETE_PHASE_LABELS: Record<GameDeletePhase["phase"], string> = {
  idle: "",
  starting: "Starting deletion…",
  polling: "Deleting game data…",
  done: "",
  error: "",
};

/** Maps an export/import phase onto the ProgressOperationDialog status prop. */
function progressStatus(phase: string): "active" | "done" | "error" {
  if (phase === "done") {
    return "done";
  }
  if (phase === "error") {
    return "error";
  }
  return "active";
}

export default function System() {
  // The signed-in username and the Log out item, shared with every other page's hamburger menu.
  const { headerLabel: accountHeader, group: accountGroup } = useAccountMenu();

  const [view, setView] = useState<SystemView>("none");
  const [users, setUsers] = useState<AuthUser[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // --- Permissions dialog state -----------------------------------------------------------------
  // The user whose permissions are being edited (null when the dialog is closed), plus its own error and
  // in-flight flag so a failed save can keep the dialog open with the edit intact.
  const [permissionsUser, setPermissionsUser] = useState<AuthUser | null>(null);
  const [permissionsError, setPermissionsError] = useState<string | null>(null);
  const [savingPermissions, setSavingPermissions] = useState(false);

  // --- Games state ------------------------------------------------------------------------------
  // The games list has its own loading and error state rather than sharing the users one, so switching
  // between the two views never shows one view's error over the other's data.
  const [games, setGames] = useState<GameSummary[]>([]);
  const [gamesLoading, setGamesLoading] = useState(false);
  const [gamesError, setGamesError] = useState<string | null>(null);

  // The game awaiting confirmation of its delete, or null when the confirmation dialog is closed.
  const [gameToDelete, setGameToDelete] = useState<GameSummary | null>(null);

  // Deleting a game is a background job: the endpoint queues it and this state machine polls it to completion.
  const gameDelete = useGameDelete();

  // The System panel itself only needs SystemPanel, so a user can be standing here without being allowed to
  // delete anything. Reading the claim once matches how PermissionGate does it — permissions only change on
  // login or a token refresh, both of which reload the page.
  const canDeleteGames = useMemo(() => hasPermission("DeleteGame"), []);

  // --- Files (catalogue export / import) state --------------------------------------------------
  // Controls the ExportSelectionDialog opened by the Files group's "Export" button, plus its error banner.
  const [showExportDialog, setShowExportDialog] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);

  // The name of the file the user picked to import, shown in the import progress dialog title.
  const [importFileName, setImportFileName] = useState("");

  // Hidden file input clicked by the "Import" button so the developer can pick an exported zip.
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  // Background export/import state machines (initiate → poll → download/done), reusing the generic poller.
  const catalogueExport = useCatalogueExport();
  const catalogueImport = useCatalogueImport();

  // Loads (or reloads) the full user list. Used both when opening the Users view and after every
  // status change so the table reflects the server's truth (timestamps, etc.).
  const loadUsers = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const fetched = await getUsers();
      setUsers(fetched);
    } catch (err) {
      // This dev tool has no toast wiring yet, so log for diagnostics and surface an inline message.
      console.error("Failed to load users.", err);
      setError("Failed to load users. See the console for details.");
    } finally {
      setLoading(false);
    }
  }, []);

  // Loads (or reloads) the full games list. Called when opening the Games view and again once a delete
  // finishes, so the deleted game drops out of the table.
  const loadGames = useCallback(async () => {
    setGamesLoading(true);
    setGamesError(null);
    try {
      const fetched = await getGames();
      setGames(fetched);
    } catch (err) {
      console.error("Failed to load games.", err);
      setGamesError("Failed to load games. See the console for details.");
    } finally {
      setGamesLoading(false);
    }
  }, []);

  // Opens the Users view and kicks off a load.
  const handleOpenUsers = useCallback(() => {
    setView("users");
    loadUsers();
  }, [loadUsers]);

  // Opens the Games view and kicks off a load.
  const handleOpenGames = useCallback(() => {
    setView("games");
    loadGames();
  }, [loadGames]);

  // Refresh the list the moment the purge reports complete, so the row disappears without waiting for the
  // developer to close the progress dialog.
  useEffect(() => {
    if (gameDelete.deletePhase.phase === "done") {
      loadGames();
    }
  }, [gameDelete.deletePhase.phase, loadGames]);

  // Applies a new reason to a user. Destructive reasons (Banned/Deleted) confirm first; everything
  // else applies immediately. On success we re-fetch so the table mirrors the server exactly.
  const handleSetReason = async (userId: number, reason: InactiveReasonType) => {
    const option = INACTIVATION_REASONS.find((o) => o.reason === reason);
    if (option?.destructive) {
      const target = users.find((u) => u.id === userId);
      const name = target?.username ?? `user ${userId}`;
      const confirmed = window.confirm(`Set ${name} to "${option.label}"? This is a destructive change.`);
      if (confirmed === false) {
        return;
      }
    }

    setError(null);
    try {
      await setUserInactive(userId, reason);
      await loadUsers();
    } catch (err) {
      console.error("Failed to update user status.", err);
      setError("Failed to update user status. See the console for details.");
    }
  };

  // Confirming the delete closes the dialog and hands the game to the background state machine, which owns
  // everything from here on: the DELETE call, the polling, and the progress dialog.
  const handleConfirmDelete = () => {
    if (gameToDelete == null) {
      return;
    }

    const gameId = gameToDelete.gameId;
    setGameToDelete(null);
    gameDelete.startDelete(gameId);
  };

  // Saving permissions re-loads the list so the row's summary reflects what was actually persisted, then
  // closes. On failure the dialog stays open with the message, so the edit isn't lost.
  const handlePermissionsCommit = async (permissions: PermissionType[]) => {
    if (permissionsUser == null) {
      return;
    }

    setPermissionsError(null);
    setSavingPermissions(true);

    try {
      await setUserPermissions(permissionsUser.id, permissions);
      await loadUsers();
      setPermissionsUser(null);
    } catch (err) {
      console.error("Failed to save user permissions.", err);
      setPermissionsError(err instanceof Error ? err.message : "Failed to save permissions.");
    } finally {
      setSavingPermissions(false);
    }
  };

  // Confirming the export dialog closes it and kicks off the background export for the chosen kinds. The
  // checkbox ids map straight onto the API's selection flags.
  const handleExportCommit = (selectedIds: string[]) => {
    setExportError(null);
    setShowExportDialog(false);
    catalogueExport.startExport({
      components: selectedIds.includes("components"),
      templates: selectedIds.includes("templates"),
    });
  };

  // Picking a file starts the background import. The input value is cleared so re-picking the same file
  // still fires onChange.
  const handleImportFileChange = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) {
      return;
    }

    setImportFileName(file.name);
    catalogueImport.startImport(file);
  };

  // A "Users" group, a "Games" group and a "Files" group for catalogue export/import. The Games group is
  // omitted entirely without the DeleteGame permission, since deleting is the only thing that page does and
  // the API would refuse even the listing.
  const toolBarGroups = useMemo<ToolBarGroup[]>(
    () => [
      {
        // This page has no menu of its own, so the hamburger exists purely to host the account
        // header and Log out, keeping them where they are on every other page.
        caption: "Menu",
        buttons: [
          {
            key: "system-menu",
            render: () => (
              <HamburgerMenu title="System menu" tooltip="Menu" header={accountHeader} groups={accountGroup ? [accountGroup] : []} />
            ),
          },
        ],
      },
      {
        caption: "Users",
        buttons: [
          {
            key: "users",
            icon: <HiUsers />,
            tooltip: "Manage Users",
            active: view === "users",
            onClick: handleOpenUsers,
          },
        ],
      },
      ...(canDeleteGames
        ? [
            {
              caption: "Games",
              buttons: [
                {
                  key: "games",
                  icon: <HiRectangleStack />,
                  tooltip: "Manage Games",
                  active: view === "games",
                  onClick: handleOpenGames,
                },
              ],
            },
          ]
        : []),
      {
        caption: "Files",
        buttons: [
          {
            key: "export-catalogue",
            icon: <FaFileExport />,
            tooltip: "Export Catalogue",
            // Open the selection dialog; the actual export starts when the user confirms their picks.
            onClick: () => {
              setExportError(null);
              setShowExportDialog(true);
            },
          },
          {
            key: "import-catalogue",
            icon: <FaFileImport />,
            tooltip: "Import Catalogue",
            // Delegate to the hidden file input; the import starts from its onChange handler.
            onClick: () => fileInputRef.current?.click(),
          },
        ],
      },
    ],
    [view, handleOpenUsers, handleOpenGames, canDeleteGames, accountHeader, accountGroup],
  );

  return (
    <div className="flex min-h-screen flex-col bg-slate-900 text-slate-200">
      <ToolBar
        groups={toolBarGroups}
        containerClassName="toolbar flex justify-start border-b border-slate-200 bg-stone-100 sticky top-0 z-10"
      />

      {view === "users" && (
        <div className="flex flex-1 flex-col gap-4 p-6">
          <div className="flex items-center justify-between">
            <h1 className="text-2xl font-semibold">Users</h1>
            <span className="text-sm text-slate-400">{users.length} total</span>
          </div>

          {error != null && <Alert variant="danger">{error}</Alert>}

          <UsersTable
            users={users}
            loading={loading}
            onSetReason={handleSetReason}
            onEditPermissions={(user) => {
              setPermissionsError(null);
              setPermissionsUser(user);
            }}
          />
        </div>
      )}

      {view === "games" && (
        <div className="flex flex-1 flex-col gap-4 p-6">
          <div className="flex items-center justify-between">
            <h1 className="text-2xl font-semibold">Games</h1>
            <span className="text-sm text-slate-400">{games.length} total</span>
          </div>

          {gamesError != null && <Alert variant="danger">{gamesError}</Alert>}

          <GamesTable
            games={games}
            loading={gamesLoading}
            deletingGameId={gameDelete.deletingGameId}
            onDelete={(game) => setGameToDelete(game)}
          />
        </div>
      )}

      {view === "none" && (
        <div className="flex flex-1 flex-col items-center justify-center gap-2">
          <h1 className="text-2xl font-semibold">System</h1>
          <p className="text-sm text-slate-400">Pick a tool from the toolbar to get started.</p>
        </div>
      )}

      {/* Hidden file input backing the Files group's Import button. */}
      <input ref={fileInputRef} type="file" accept=".zip" className="hidden" onChange={handleImportFileChange} />

      <UserPermissionsDialog
        open={permissionsUser != null}
        user={permissionsUser}
        saving={savingPermissions}
        error={permissionsError}
        onCancel={() => {
          setPermissionsUser(null);
          setPermissionsError(null);
        }}
        onCommit={handlePermissionsCommit}
      />

      {/* Deleting a game is irreversible and takes the whole world with it, so it is spelled out before it runs. */}
      <ConfirmDialog
        open={gameToDelete != null}
        title="Delete game"
        confirmLabel="Delete"
        message={
          <>
            <p>
              Permanently delete <strong>{gameToDelete?.gameName}</strong>?
            </p>
            <p className="mt-2">
              Its players, campaign and battle maps, units, orders, structures, battles and turn queues all go with it.
              This cannot be undone.
            </p>
          </>
        }
        onCancel={() => setGameToDelete(null)}
        onConfirm={handleConfirmDelete}
      />

      {/* Delete progress. Driven by the delete state machine; closing resets it to idle. */}
      <ProgressOperationDialog
        open={gameDelete.isActive}
        title="Deleting game"
        operationLabel={DELETE_PHASE_LABELS[gameDelete.deletePhase.phase]}
        progressPercent={gameDelete.progressPercent}
        status={progressStatus(gameDelete.deletePhase.phase)}
        successMessage="The game and everything in it has been deleted."
        errorMessage={gameDelete.deletePhase.phase === "error" ? gameDelete.deletePhase.message : undefined}
        onClose={gameDelete.resetDelete}
      />

      <ExportSelectionDialog
        open={showExportDialog}
        onCancel={() => {
          setShowExportDialog(false);
          setExportError(null);
        }}
        onCommit={handleExportCommit}
        error={exportError}
      />

      {/* Export progress + auto-download. Driven by the export state machine; closing resets it to idle. */}
      <ProgressOperationDialog
        open={catalogueExport.isActive}
        title="Exporting catalogue"
        operationLabel={EXPORT_PHASE_LABELS[catalogueExport.exportPhase.phase]}
        progressPercent={catalogueExport.progressPercent}
        status={progressStatus(catalogueExport.exportPhase.phase)}
        successMessage="Export complete. Your download has started."
        errorMessage={catalogueExport.exportPhase.phase === "error" ? catalogueExport.exportPhase.message : undefined}
        onClose={catalogueExport.resetExport}
      />

      {/* Import progress. Driven by the import state machine; closing resets it to idle. */}
      <ProgressOperationDialog
        open={catalogueImport.isActive}
        title={importFileName ? `Importing "${importFileName}"` : "Importing catalogue"}
        operationLabel={IMPORT_PHASE_LABELS[catalogueImport.importPhase.phase]}
        progressPercent={catalogueImport.progressPercent}
        status={progressStatus(catalogueImport.importPhase.phase)}
        successMessage="Import complete. The catalogue has been updated."
        errorMessage={catalogueImport.importPhase.phase === "error" ? catalogueImport.importPhase.message : undefined}
        onClose={catalogueImport.resetImport}
      />
    </div>
  );
}
