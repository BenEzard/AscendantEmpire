import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import UserPermissionsDialog from "../UserPermissionsDialog";
import { PERMISSION_LABELS, type PermissionType } from "../../types/Permission";
import type { AuthUser } from "../../types/User";

// Purely presentational: props in, onCommit out. The dialog owns only its draft selection — the parent
// makes the API call — so no network mocking is needed.

function makeUser(permissions: PermissionType[]): AuthUser {
  return {
    id: 7,
    username: "test-user",
    email: null,
    createdAt: "2026-01-01T00:00:00+00:00",
    inactiveAt: null,
    inactiveReasonType: "Active",
    permissions,
  };
}

function checkboxFor(permission: PermissionType): HTMLInputElement {
  return screen.getByRole("checkbox", { name: new RegExp(PERMISSION_LABELS[permission]) });
}

// ─── Seeding ──────────────────────────────────────────────────────────────────

describe("seeding", () => {
  it("ticks exactly the permissions the user already holds", () => {
    render(<UserPermissionsDialog open user={makeUser(["CreateGame", "MapEditor"])} onCancel={vi.fn()} onCommit={vi.fn()} />);

    expect(checkboxFor("CreateGame").checked).toBe(true);
    expect(checkboxFor("MapEditor").checked).toBe(true);
    expect(checkboxFor("SystemPanel").checked).toBe(false);
  });

  it("re-seeds from the user each time it opens, so a previous edit does not leak in", async () => {
    const user = userEvent.setup();
    const held = makeUser(["CreateGame"]);

    const { rerender } = render(<UserPermissionsDialog open user={held} onCancel={vi.fn()} onCommit={vi.fn()} />);

    await user.click(checkboxFor("SystemPanel"));
    expect(checkboxFor("SystemPanel").checked).toBe(true);

    // Close, then reopen on the same unchanged user.
    rerender(<UserPermissionsDialog open={false} user={held} onCancel={vi.fn()} onCommit={vi.fn()} />);
    rerender(<UserPermissionsDialog open user={held} onCancel={vi.fn()} onCommit={vi.fn()} />);

    expect(checkboxFor("SystemPanel").checked).toBe(false);
    expect(checkboxFor("CreateGame").checked).toBe(true);
  });
});

// ─── Committing ───────────────────────────────────────────────────────────────

describe("committing", () => {
  it("sends the complete selection, not just the changes", async () => {
    const user = userEvent.setup();
    const onCommit = vi.fn();

    render(<UserPermissionsDialog open user={makeUser(["CreateGame"])} onCancel={vi.fn()} onCommit={onCommit} />);

    await user.click(checkboxFor("MapEditor"));
    await user.click(screen.getByRole("button", { name: "Save" }));

    expect(onCommit).toHaveBeenCalledTimes(1);
    expect(onCommit.mock.calls[0][0]).toEqual(expect.arrayContaining(["CreateGame", "MapEditor"]));
    expect(onCommit.mock.calls[0][0]).toHaveLength(2);
  });

  it("sends an empty list when every box is unticked, since that revokes everything", async () => {
    const user = userEvent.setup();
    const onCommit = vi.fn();

    render(<UserPermissionsDialog open user={makeUser(["CreateGame"])} onCancel={vi.fn()} onCommit={onCommit} />);

    await user.click(checkboxFor("CreateGame"));
    await user.click(screen.getByRole("button", { name: "Save" }));

    expect(onCommit).toHaveBeenCalledWith([]);
  });

  it("does not commit when cancelled", async () => {
    const user = userEvent.setup();
    const onCommit = vi.fn();
    const onCancel = vi.fn();

    render(<UserPermissionsDialog open user={makeUser([])} onCancel={onCancel} onCommit={onCommit} />);

    await user.click(checkboxFor("SystemPanel"));
    await user.click(screen.getByRole("button", { name: "Cancel" }));

    expect(onCancel).toHaveBeenCalledTimes(1);
    expect(onCommit).not.toHaveBeenCalled();
  });
});

// ─── Saving state ─────────────────────────────────────────────────────────────

describe("saving", () => {
  it("disables the controls while a save is in flight so it cannot be submitted twice", () => {
    render(<UserPermissionsDialog open user={makeUser([])} saving onCancel={vi.fn()} onCommit={vi.fn()} />);

    expect(screen.getByRole("button", { name: "Saving..." })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Cancel" })).toBeDisabled();
    expect(checkboxFor("SystemPanel")).toBeDisabled();
  });

  it("shows the server's message when a save fails", () => {
    render(<UserPermissionsDialog open user={makeUser([])} error="User 7 was not found." onCancel={vi.fn()} onCommit={vi.fn()} />);

    expect(screen.getByText("User 7 was not found.")).toBeInTheDocument();
  });
});
