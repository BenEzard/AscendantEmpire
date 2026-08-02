import type { ReactNode } from "react";
import { hasPermission } from "@/api/authToken";
import { PERMISSION_LABELS, type PermissionType } from "@/features/system/types/Permission";

export interface PermissionGateProps {
  /** The permission the page requires. */
  require: PermissionType;
  children: ReactNode;
}

/**
 * Hides a page from a logged-in user who lacks the permission it needs, showing why instead.
 *
 * This is presentation, not protection — the API refuses the same calls with a 403 regardless of what
 * the browser believes. Its job is to replace a screen full of failed requests with one clear message.
 * Compose it inside AuthGate, which handles the not-logged-in case.
 */
export default function PermissionGate({ require, children }: PermissionGateProps) {
  // Read once on mount, like AuthGate does. Permissions only change on login, enter-game or a token
  // refresh, and the first two reload the page anyway.
  if (hasPermission(require)) {
    return <>{children}</>;
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-900 p-6 text-slate-200">
      <div className="max-w-md rounded-lg border border-slate-700 bg-slate-800 p-6 text-center">
        <h1 className="mb-2 text-lg font-semibold">No access to this tool</h1>

        <p className="text-sm text-slate-400">
          Your account does not have the <strong>{PERMISSION_LABELS[require]}</strong> permission. Ask an administrator to grant it from
          System &rsaquo; Users.
        </p>
      </div>
    </div>
  );
}
