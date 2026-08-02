// Read-only admin grid of users for the dev System tool. Each row shows the user's identity and
// current status, plus a set of action buttons that apply one of the inactivation reasons (and a
// "Reactivate" button for users that are already inactive).
//
// We hand-roll a <table> rather than reuse the shared EditableTable, which is built around growable
// add/remove-row lists and doesn't fit a fixed admin grid with per-row action buttons.

import { Button, Badge, Spinner } from "@/components/ui";

import {
  InactiveReason,
  INACTIVATION_REASONS,
  REASON_LABELS,
  isActive,
} from "../types/User";
import type { AuthUser, InactiveReasonType } from "../types/User";

interface UsersTableProps {
  users: AuthUser[];
  loading: boolean;
  /** Called when an admin picks a new reason (or Reactivate) for a user. */
  onSetReason: (userId: number, reason: InactiveReasonType) => void;
  /** Called when an admin opens the permissions dialog for a user. */
  onEditPermissions: (user: AuthUser) => void;
}

/** Formats an ISO timestamp as a short local date/time, or an em dash when absent. */
function formatDate(value: string | null): string {
  if (value == null) {
    return "—";
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleString();
}

/** Colors for the status badge: green while active, red once inactive. */
function statusColors(active: boolean): { textColor: string; backgroundColor: string } {
  if (active) {
    return { textColor: "#14532d", backgroundColor: "#bbf7d0" };
  }

  return { textColor: "#7f1d1d", backgroundColor: "#fecaca" };
}

export default function UsersTable({ users, loading, onSetReason, onEditPermissions }: UsersTableProps) {
  // While the first load is in flight, show a spinner instead of an empty grid.
  if (loading && users.length === 0) {
    return (
      <div className="flex items-center justify-center gap-2 py-12 text-slate-400">
        <Spinner />
        <span>Loading users…</span>
      </div>
    );
  }

  if (users.length === 0) {
    return <p className="py-12 text-center text-sm text-slate-400">No users found.</p>;
  }

  return (
    <div className="overflow-x-auto rounded-lg border border-slate-700">
      <table className="w-full border-collapse text-sm">
        <thead>
          <tr className="bg-slate-800 text-left text-slate-300">
            <th className="px-3 py-2 font-semibold">ID</th>
            <th className="px-3 py-2 font-semibold">Username</th>
            <th className="px-3 py-2 font-semibold">Email</th>
            <th className="px-3 py-2 font-semibold">Created</th>
            <th className="px-3 py-2 font-semibold">Status</th>
            <th className="px-3 py-2 font-semibold">Permissions</th>
            <th className="px-3 py-2 font-semibold">Actions</th>
          </tr>
        </thead>

        <tbody>
          {users.map((user) => {
            const active = isActive(user);
            const colors = statusColors(active);

            return (
              <tr key={user.id} className="border-t border-slate-700 text-slate-200">
                <td className="px-3 py-2 text-slate-400">{user.id}</td>
                <td className="px-3 py-2 font-medium">{user.username}</td>
                <td className="px-3 py-2 text-slate-300">{user.email ?? "—"}</td>
                <td className="px-3 py-2 text-slate-400">{formatDate(user.createdAt)}</td>
                <td className="px-3 py-2">
                  <Badge
                    label={REASON_LABELS[user.inactiveReasonType]}
                    textColor={colors.textColor}
                    backgroundColor={colors.backgroundColor}
                    tooltipText={active ? undefined : `Inactive since ${formatDate(user.inactiveAt)}`}
                  />
                  {/* Show when the account went inactive as quiet secondary text. */}
                  {active === false && (
                    <span className="ml-2 text-xs text-slate-500">{formatDate(user.inactiveAt)}</span>
                  )}
                </td>
                <td className="px-3 py-2">
                  {/* Summarised rather than listed in full: five labels per row would crowd out the actions,
                      and the dialog is one click away for the detail. */}
                  <div className="flex items-center gap-2">
                    <span className="text-slate-400">
                      {user.permissions.length === 0 ? "None" : `${user.permissions.length} granted`}
                    </span>
                    <Button
                      variant="outline-secondary"
                      className="px-2 py-1 text-xs"
                      onClick={() => onEditPermissions(user)}>
                      Edit
                    </Button>
                  </div>
                </td>
                <td className="px-3 py-2">
                  <div className="flex flex-wrap items-center gap-2">
                    {/* Reactivate is only meaningful for an already-inactive account. */}
                    {active === false && (
                      <Button
                        variant="success"
                        className="px-2 py-1 text-xs"
                        onClick={() => onSetReason(user.id, InactiveReason.Active)}>
                        Reactivate
                      </Button>
                    )}

                    {/* One button per inactivation reason. The current reason is disabled (no-op);
                        destructive reasons use the danger variant and ask for confirmation upstream. */}
                    {INACTIVATION_REASONS.map((option) => (
                      <Button
                        key={option.reason}
                        variant={option.destructive ? "danger" : "secondary"}
                        className="px-2 py-1 text-xs"
                        disabled={user.inactiveReasonType === option.reason}
                        onClick={() => onSetReason(user.id, option.reason)}>
                        {option.label}
                      </Button>
                    ))}
                  </div>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
