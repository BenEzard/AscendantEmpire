// Wiring tests for TagSetupDialog: the commit gate, which page a failed save lands on, and the re-seed
// on reopen. The validation *rules* themselves are pure functions on the model and are covered directly
// in features/unitdesign/types/__tests__/Tag.test.ts — this file deliberately doesn't re-test them, only
// that the dialog consults them and reacts correctly.
//
// Purely presentational: props in, onCommit out. The parent makes the API call, so no network mocking.

import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import TagSetupDialog from "../TagSetupDialog";

// The dialog falls back to the raw enum names when the tag type catalogue is empty, which keeps the
// option values and labels identical and the queries below independent of any seeded display names.
function renderDialog(props: Partial<React.ComponentProps<typeof TagSetupDialog>> = {}) {
  const onCommit = vi.fn();
  const onCancel = vi.fn();
  const all = { open: true, tagTypes: [], onCommit, onCancel, ...props };

  const { rerender } = render(<TagSetupDialog {...all} />);

  return {
    onCommit,
    onCancel,
    // Re-render with the same props unless overridden — used to close and reopen the dialog.
    rerender: (next: Partial<React.ComponentProps<typeof TagSetupDialog>> = {}) => rerender(<TagSetupDialog {...all} {...next} />),
  };
}

// FieldRow renders its label as a plain <label> with no htmlFor and the control as a sibling, so the
// controls aren't reachable by label text; placeholders are the stable handle.
const displayNameInput = () => screen.getByPlaceholderText("e.g. Amphibious");
const codeInput = () => screen.getByPlaceholderText("tag code, e.g. amphibious");
const saveButton = () => screen.getByRole("button", { name: "Save" });

// Nav buttons render as "<number> <label>", so match on the label portion.
const generalTab = () => screen.getByRole("button", { name: /General/ });
const specificsTab = () => screen.getByRole("button", { name: /Tag Specifics/ });

/** The nav button of the page currently on screen, identified by aria-current. */
function activeTabName(): string {
  const current = screen.getAllByRole("button").find((button) => button.getAttribute("aria-current") === "page");
  return current?.textContent?.trim() ?? "";
}

/** The <select> owning a given option — the selects have no accessible name to query by. */
function selectWithOption(optionName: string): HTMLSelectElement {
  const option = screen.getByRole("option", { name: optionName });
  const select = option.closest("select");
  if (!select) {
    throw new Error(`No <select> found for option "${optionName}"`);
  }
  return select;
}

/** Fills in the name (which auto-derives the code), leaving the tag valid as a plain Classification. */
async function nameTheTag(user: ReturnType<typeof userEvent.setup>, name = "Amphibious") {
  await user.type(displayNameInput(), name);
}

/** Turns the tag into a MilitaryOrganisationalGroup/Unled one, whose three sizes start blank (invalid). */
async function makeUnled(user: ReturnType<typeof userEvent.setup>) {
  await user.selectOptions(selectWithOption("MilitaryOrganisationalGroup"), "MilitaryOrganisationalGroup");
  await user.selectOptions(selectWithOption("Unled"), "Unled");
}

// ─── The commit gate ──────────────────────────────────────────────────────────

describe("the commit gate", () => {
  it("commits the tag once the required fields are filled in", async () => {
    const user = userEvent.setup();
    const { onCommit } = renderDialog();

    await nameTheTag(user);
    await user.click(saveButton());

    expect(onCommit).toHaveBeenCalledTimes(1);
    expect(onCommit.mock.calls[0][0]).toMatchObject({ displayName: "Amphibious", code: "amphibious", tagType: "Classification" });
  });

  it("does not commit while a required field is blank", async () => {
    const user = userEvent.setup();
    const { onCommit } = renderDialog();

    // A freshly opened dialog has neither a display name nor a code.
    await user.click(saveButton());

    expect(onCommit).not.toHaveBeenCalled();
  });

  it("does not commit an Unled tag whose sizes are incomplete", async () => {
    const user = userEvent.setup();
    const { onCommit } = renderDialog();

    await nameTheTag(user);
    await makeUnled(user);
    await user.click(saveButton());

    expect(onCommit).not.toHaveBeenCalled();
  });

  it("commits an Unled tag once its sizes cohere", async () => {
    const user = userEvent.setup();
    const { onCommit } = renderDialog();

    await nameTheTag(user, "Settler Band");
    await makeUnled(user);
    await user.type(screen.getByPlaceholderText("base"), "10");
    await user.type(screen.getByPlaceholderText("default"), "20");
    await user.type(screen.getByPlaceholderText("max"), "100");
    await user.click(saveButton());

    expect(onCommit).toHaveBeenCalledTimes(1);
    expect(onCommit.mock.calls[0][0]).toMatchObject({
      tagType: "MilitaryOrganisationalGroup",
      leadershipModel: "Unled",
      unledBaseSize: 10,
      unledDefaultSize: 20,
      unledMaxSize: 100,
    });
  });

  it("keeps validation messages hidden until a save has been attempted", async () => {
    const user = userEvent.setup();
    renderDialog();

    expect(screen.queryByText("Display Name is required.")).not.toBeInTheDocument();

    await user.click(saveButton());

    expect(screen.getByText("Display Name is required.")).toBeInTheDocument();
  });

  it("does not commit when cancelled", async () => {
    const user = userEvent.setup();
    const { onCommit, onCancel } = renderDialog();

    await nameTheTag(user);
    await user.click(screen.getByRole("button", { name: "Cancel / Close" }));

    expect(onCancel).toHaveBeenCalledTimes(1);
    expect(onCommit).not.toHaveBeenCalled();
  });
});

// ─── Where a failed save lands ────────────────────────────────────────────────
//
// Only one page is on screen at a time, so a failed save whose error sits on the *other* page would
// look like Save silently doing nothing. These pin the navigation that prevents that.

describe("where a failed save lands", () => {
  it("opens on the General page", () => {
    renderDialog();

    expect(activeTabName()).toContain("General");
  });

  it("moves to Tag Specifics when only the unled sizing is wrong", async () => {
    const user = userEvent.setup();
    renderDialog();

    await nameTheTag(user);
    await makeUnled(user);
    // Go back to General, so the sizing error is on the page the user cannot see.
    await user.click(generalTab());
    expect(activeTabName()).toContain("General");

    await user.click(saveButton());

    expect(activeTabName()).toContain("Tag Specifics");
    expect(screen.getByText(/requires all three sizes/)).toBeInTheDocument();
  });

  it("moves back to General when a required field there is blank", async () => {
    const user = userEvent.setup();
    renderDialog();

    await user.click(specificsTab());
    expect(activeTabName()).toContain("Tag Specifics");

    await user.click(saveButton());

    expect(activeTabName()).toContain("General");
  });

  it("prefers General when both pages have a problem, since it comes first", async () => {
    const user = userEvent.setup();
    renderDialog();

    // Unled with blank sizes, and then the display name emptied so General is invalid too.
    await nameTheTag(user);
    await makeUnled(user);
    await user.clear(displayNameInput());
    await user.click(specificsTab());

    await user.click(saveButton());

    expect(activeTabName()).toContain("General");
  });

  it("stays on General for a blank code, which is a General-page field", async () => {
    const user = userEvent.setup();
    renderDialog();

    await nameTheTag(user);
    // Hand-editing the code stops it tracking the display name, so it can be left blank.
    await user.clear(codeInput());

    await user.click(saveButton());

    expect(activeTabName()).toContain("General");
    expect(screen.getByText("Code is required.")).toBeInTheDocument();
  });

  it("marks the offending tab so the error is visible from the other page", async () => {
    const user = userEvent.setup();
    renderDialog();

    // Nothing is marked before a save is attempted.
    expect(screen.queryByRole("img", { name: "has validation errors" })).not.toBeInTheDocument();

    await user.click(saveButton());

    expect(generalTab()).toContainElement(screen.getByRole("img", { name: "has validation errors" }));
  });
});

// ─── Reopening ────────────────────────────────────────────────────────────────

describe("reopening", () => {
  it("re-seeds the form, so a previous edit does not leak into the next Add", async () => {
    const user = userEvent.setup();
    const { rerender } = renderDialog();

    await nameTheTag(user);
    expect(displayNameInput()).toHaveValue("Amphibious");

    rerender({ open: false });
    rerender({ open: true });

    expect(displayNameInput()).toHaveValue("");
    expect(codeInput()).toHaveValue("");
  });

  it("starts validation quiet again, rather than reopening pre-littered with errors", async () => {
    const user = userEvent.setup();
    const { rerender } = renderDialog();

    await user.click(saveButton());
    expect(screen.getByText("Display Name is required.")).toBeInTheDocument();

    rerender({ open: false });
    rerender({ open: true });

    expect(screen.queryByText("Display Name is required.")).not.toBeInTheDocument();
  });

  it("returns to the General page", async () => {
    const user = userEvent.setup();
    const { rerender } = renderDialog();

    await user.click(specificsTab());
    expect(activeTabName()).toContain("Tag Specifics");

    rerender({ open: false });
    rerender({ open: true });

    expect(activeTabName()).toContain("General");
  });
});

// ─── The title ────────────────────────────────────────────────────────────────

describe("the title", () => {
  it("names the tag being edited once it has a display name", async () => {
    const user = userEvent.setup();
    renderDialog();

    expect(screen.getByText("Tag Setup")).toBeInTheDocument();

    await nameTheTag(user);

    expect(screen.getByText("Tag Setup: Amphibious")).toBeInTheDocument();
  });

  it("falls back to the bare title rather than showing a dangling colon", async () => {
    const user = userEvent.setup();
    renderDialog();

    // Whitespace alone is not a name.
    await user.type(displayNameInput(), "   ");

    expect(screen.getByText("Tag Setup")).toBeInTheDocument();
  });
});
