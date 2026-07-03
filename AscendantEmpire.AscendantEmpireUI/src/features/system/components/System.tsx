// Developer-only "System" tool (a standalone dev tool like the Map Editor — no AuthGate). Its first
// feature is user administration: a toolbar "Users" button loads every user into a table where each
// account can be inactivated for a reason (Quit/Banned/Deleted) or reactivated.
import { useMemo, useState } from "react";
import { HiUsers } from "react-icons/hi2";

import ToolBar from "@/components/Toolbar";
import type { ToolBarGroup } from "@/components/Toolbar";
import { Alert } from "@/components/ui";

import { getUsers, setUserInactive } from "../api/usersApi";
import { INACTIVATION_REASONS } from "../types/User";
import type { AuthUser, InactiveReasonType } from "../types/User";
import UsersTable from "./UsersTable";

// Which panel the page is currently showing. Only the Users view exists for now, but this leaves room
// for more System tools (each toolbar group can flip to its own view).
type SystemView = "none" | "users";

export default function System() {
  const [view, setView] = useState<SystemView>("none");
  const [users, setUsers] = useState<AuthUser[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Loads (or reloads) the full user list. Used both when opening the Users view and after every
  // status change so the table reflects the server's truth (timestamps, etc.).
  const loadUsers = async () => {
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
  };

  // Opens the Users view and kicks off a load.
  const handleOpenUsers = () => {
    setView("users");
    loadUsers();
  };

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

  // A single "Users" group for now. The button opens the user-management table.
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
    ],
    [view],
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

          <UsersTable users={users} loading={loading} onSetReason={handleSetReason} />
        </div>
      ) : (
        <div className="flex flex-1 flex-col items-center justify-center gap-2">
          <h1 className="text-2xl font-semibold">System</h1>
          <p className="text-sm text-slate-400">Pick a tool from the toolbar to get started.</p>
        </div>
      )}
    </div>
  );
}
