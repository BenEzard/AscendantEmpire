// Developer-only "System" tool (a standalone dev tool like the Map Editor), gated behind login via
// AuthGate since its user-administration endpoints require an authenticated bearer token. Its first
// feature is user administration: a toolbar "Users" button loads every user into a table where each
// account can be inactivated for a reason (Quit/Banned/Deleted) or reactivated.
import { useCallback, useMemo, useRef, useState, type ChangeEvent } from "react";
import { HiUsers } from "react-icons/hi2";
import { FaFileExport, FaFileImport } from "react-icons/fa6";

import ToolBar from "@/components/ui/toolbar/Toolbar";
import type { ToolBarGroup } from "@/components/ui/toolbar/Toolbar";
import { Alert, ProgressOperationDialog } from "@/components/ui";

import { getUsers, setUserInactive, setUserPermissions } from "../api/usersApi";
import { INACTIVATION_REASONS } from "../types/User";
import type { AuthUser, InactiveReasonType } from "../types/User";
import type { PermissionType } from "../types/Permission";
import UsersTable from "./UsersTable";
import UserPermissionsDialog from "./UserPermissionsDialog";
import ExportSelectionDialog from "./ExportSelectionDialog";
import { useCatalogExport, type CatalogExportPhase } from "../hooks/useCatalogExport";
import { useCatalogImport, type CatalogImportPhase } from "../hooks/useCatalogImport";

// Which panel the page is currently showing. Only the Users view exists for now, but this leaves room
// for more System tools (each toolbar group can flip to its own view).
type SystemView = "none" | "users";

// Short status labels shown under the progress bar for each phase of the export/import state machines.
const EXPORT_PHASE_LABELS: Record<CatalogExportPhase["phase"], string> = {
  idle: "",
  starting: "Starting export…",
  polling: "Export in progress…",
  downloading: "Preparing download…",
  done: "",
  error: "",
};

const IMPORT_PHASE_LABELS: Record<CatalogImportPhase["phase"], string> = {
  idle: "",
  starting: "Starting import…",
  polling: "Import in progress…",
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

  // --- Files (catalogue export / import) state --------------------------------------------------
  // Controls the ExportSelectionDialog opened by the Files group's "Export" button, plus its error banner.
  const [showExportDialog, setShowExportDialog] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);

  // The name of the file the user picked to import, shown in the import progress dialog title.
  const [importFileName, setImportFileName] = useState("");

  // Hidden file input clicked by the "Import" button so the developer can pick an exported zip.
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  // Background export/import state machines (initiate → poll → download/done), reusing the generic poller.
  const catalogExport = useCatalogExport();
  const catalogImport = useCatalogImport();

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

  // Opens the Users view and kicks off a load.
  const handleOpenUsers = useCallback(() => {
    setView("users");
    loadUsers();
  }, [loadUsers]);

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
    catalogExport.startExport({
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
    catalogImport.startImport(file);
  };

  // A "Users" group plus a "Files" group for catalogue export/import. The Users button opens the
  // user-management table; the Files buttons drive the background export/import state machines.
  const toolBarGroups = useMemo<ToolBarGroup[]>(
    () => [
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
    [view, handleOpenUsers],
  );

  return (
    <div className="flex min-h-screen flex-col bg-slate-900 text-slate-200">
      <ToolBar
        groups={toolBarGroups}
        containerClassName="toolbar flex justify-start border-b border-slate-200 bg-stone-100 sticky top-0 z-10"
      />

      {view === "users" ? (
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
      ) : (
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
        open={catalogExport.isActive}
        title="Exporting catalogue"
        operationLabel={EXPORT_PHASE_LABELS[catalogExport.exportPhase.phase]}
        progressPercent={catalogExport.progressPercent}
        status={progressStatus(catalogExport.exportPhase.phase)}
        successMessage="Export complete. Your download has started."
        errorMessage={catalogExport.exportPhase.phase === "error" ? catalogExport.exportPhase.message : undefined}
        onClose={catalogExport.resetExport}
      />

      {/* Import progress. Driven by the import state machine; closing resets it to idle. */}
      <ProgressOperationDialog
        open={catalogImport.isActive}
        title={importFileName ? `Importing "${importFileName}"` : "Importing catalogue"}
        operationLabel={IMPORT_PHASE_LABELS[catalogImport.importPhase.phase]}
        progressPercent={catalogImport.progressPercent}
        status={progressStatus(catalogImport.importPhase.phase)}
        successMessage="Import complete. The catalogue has been updated."
        errorMessage={catalogImport.importPhase.phase === "error" ? catalogImport.importPhase.message : undefined}
        onClose={catalogImport.resetImport}
      />
    </div>
  );
}
