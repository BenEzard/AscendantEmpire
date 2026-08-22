import { useEffect, useState } from "react";
import { Modal, Button, Alert, dialogTitle } from "@/components/ui";

import { PERMISSIONS, PERMISSION_DESCRIPTIONS, PERMISSION_LABELS, type PermissionType } from "../types/Permission";
import type { AuthUser } from "../types/User";

export interface UserPermissionsDialogProps {
  open: boolean;
  /** The user being edited, or null when the dialog is closed. */
  user: AuthUser | null;
  onCancel: () => void;
  /** Called with the complete set of permissions the user should end up with. */
  onCommit: (permissions: PermissionType[]) => void;
  error?: string | null;
  /** True while the save is in flight, so the buttons can be disabled. */
  saving?: boolean;
}

/**
 * Edits the capabilities granted to one account.
 *
 * The ticked boxes are the complete desired state, not a set of changes: unticking a box revokes that
 * permission. Selection is re-seeded from the user each time the dialog opens so a previous edit never
 * leaks into the next one.
 */
export default function UserPermissionsDialog({ open, user, onCancel, onCommit, error, saving = false }: UserPermissionsDialogProps) {
  const [selected, setSelected] = useState<Set<PermissionType>>(new Set());

  // Re-seed from the user being edited on every open.
  useEffect(() => {
    if (open) {
      setSelected(new Set(user?.permissions ?? []));
    }
  }, [open, user]);

  const handleToggle = (permission: PermissionType) => {
    setSelected((previous) => {
      const next = new Set(previous);
      if (next.has(permission)) {
        next.delete(permission);
      } else {
        next.add(permission);
      }
      return next;
    });
  };

  return (
    <Modal
      open={open}
      onClose={onCancel}
      // The account being edited names the dialog; with no user selected the bare "Permissions" is kept.
      title={dialogTitle("Permissions", user?.username)}
      size="md"
      footer={
        <>
          <Button variant="secondary" onClick={onCancel} disabled={saving}>
            Cancel
          </Button>
          <Button variant="success" onClick={() => onCommit([...selected])} disabled={saving}>
            {saving ? "Saving..." : "Save"}
          </Button>
        </>
      }>
      {error && (
        <Alert variant="danger" className="mb-3">
          {error}
        </Alert>
      )}

      <p className="mb-3 text-sm text-slate-600">
        Tick the capabilities this account should have. Changes take effect for the user within a few minutes, when their session next refreshes.
      </p>

      <div className="space-y-1">
        {PERMISSIONS.map((permission) => (
          <label
            key={permission}
            className="flex items-start gap-2 rounded px-2 py-1.5 text-sm text-slate-700 hover:bg-slate-50">
            <input
              type="checkbox"
              className="mt-1"
              checked={selected.has(permission)}
              disabled={saving}
              onChange={() => handleToggle(permission)}
            />
            <span>
              <span className="font-medium">{PERMISSION_LABELS[permission]}</span>
              <span className="block text-xs text-slate-500">{PERMISSION_DESCRIPTIONS[permission]}</span>
            </span>
          </label>
        ))}
      </div>
    </Modal>
  );
}
