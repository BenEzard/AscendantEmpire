import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import SeedSelectionDialog from "../SeedSelectionDialog";
import type { SeedTarget, SeedTargetInfo } from "../../api/seedApi";

// Purely presentational: props in, onCommit out.
// The dialog owns only its draft selection — the parent makes the API call and the server expands the selection again — so no network mocking is needed.

/** The real graph's shape: three independent map targets plus the four-deep unit-design chain. */
const TARGETS: SeedTargetInfo[] = [
  { target: "Geographies", group: "Map", displayName: "Geographies", description: "Terrain elevation.", dependsOn: [], sortOrder: 10, itemCount: 6 },
  { target: "Biomes", group: "Map", displayName: "Biomes", description: "Terrain cover.", dependsOn: [], sortOrder: 20, itemCount: 12 },
  { target: "MapResourceCategories", group: "Map", displayName: "Map Resource Categories", description: "Asset groups.", dependsOn: [], sortOrder: 30, itemCount: 7 },
  { target: "TagTypes", group: "UnitDesign", displayName: "Tag Types", description: "Tag classifications.", dependsOn: [], sortOrder: 10, itemCount: 5 },
  { target: "Tags", group: "UnitDesign", displayName: "Tags", description: "The tag catalogue.", dependsOn: ["TagTypes"], sortOrder: 20, itemCount: 5 },
  { target: "UnitComponents", group: "UnitDesign", displayName: "Unit Components", description: "Bodies and equipment.", dependsOn: ["Tags"], sortOrder: 30, itemCount: 6 },
  { target: "StarterTemplates", group: "UnitDesign", displayName: "Starter Unit Templates", description: "Settler and scout.", dependsOn: ["UnitComponents"], sortOrder: 40, itemCount: 2 },
];

// Anchored to the start of the accessible name, which begins with the display name. 
// An unanchored match would be ambiguous once a row is locked: "Tag Types" then reads "…Required by Tags", which a bare /Tags/ would also match.
function checkboxFor(target: SeedTarget): HTMLInputElement {
  const label = TARGETS.find((candidate) => candidate.target === target)!.displayName;
  return screen.getByRole("checkbox", { name: new RegExp(`^${label}`) });
}

function renderDialog(overrides: Partial<React.ComponentProps<typeof SeedSelectionDialog>> = {}) {
  const onCommit = vi.fn();

  render(<SeedSelectionDialog open targets={TARGETS} onCancel={vi.fn()} onCommit={onCommit} {...overrides} />);

  return { onCommit };
}

// ─── Dependency closure ───────────────────────────────────────────────────────

describe("dependency closure", () => {
  it("ticks and locks every prerequisite of a ticked target", async () => {
    const user = userEvent.setup();
    renderDialog();

    await user.click(checkboxFor("StarterTemplates"));

    // The whole chain beneath it comes along, and cannot be released on its own.
    for (const target of ["UnitComponents", "Tags", "TagTypes"] as const) {
      expect(checkboxFor(target).checked).toBe(true);
      expect(checkboxFor(target).disabled).toBe(true);
    }

    // The target the user actually ticked stays theirs to untick.
    expect(checkboxFor("StarterTemplates").disabled).toBe(false);
  });

  it("names what pulled a locked target in, so the lock is explained rather than just imposed", async () => {
    const user = userEvent.setup();
    renderDialog();

    await user.click(checkboxFor("StarterTemplates"));

    expect(screen.getAllByText(/Required by Starter Unit Templates/).length).toBeGreaterThan(0);
  });

  it("releases prerequisites when the target that needed them is unticked", async () => {
    const user = userEvent.setup();
    renderDialog();

    await user.click(checkboxFor("StarterTemplates"));
    await user.click(checkboxFor("StarterTemplates"));

    expect(checkboxFor("UnitComponents").checked).toBe(false);
    expect(checkboxFor("UnitComponents").disabled).toBe(false);
    expect(checkboxFor("TagTypes").checked).toBe(false);
  });

  it("keeps a target unlocked when the user ticked it themselves as well", async () => {
    const user = userEvent.setup();
    renderDialog();

    await user.click(checkboxFor("Tags"));
    await user.click(checkboxFor("StarterTemplates"));

    // Tags is now both explicitly ticked and implied; explicit wins, so it stays editable.
    expect(checkboxFor("Tags").disabled).toBe(false);
    expect(checkboxFor("TagTypes").disabled).toBe(true);
  });

  it("leaves independent targets alone", async () => {
    const user = userEvent.setup();
    renderDialog();

    await user.click(checkboxFor("Biomes"));

    expect(checkboxFor("Geographies").checked).toBe(false);
    expect(checkboxFor("TagTypes").checked).toBe(false);
  });
});

// ─── Committing ───────────────────────────────────────────────────────────────

describe("committing", () => {
  it("submits the ticked targets closed over their prerequisites", async () => {
    const user = userEvent.setup();
    const { onCommit } = renderDialog();

    await user.click(checkboxFor("StarterTemplates"));
    await user.click(screen.getByRole("button", { name: /Seed 4 Targets/ }));

    expect(onCommit).toHaveBeenCalledWith(["TagTypes", "Tags", "UnitComponents", "StarterTemplates"]);
  });

  it("submits a single independent target on its own", async () => {
    const user = userEvent.setup();
    const { onCommit } = renderDialog();

    await user.click(checkboxFor("Biomes"));
    await user.click(screen.getByRole("button", { name: /Seed 1 Target/ }));

    expect(onCommit).toHaveBeenCalledWith(["Biomes"]);
  });

  it("disables Seed until something is ticked, so an empty selection cannot be sent", async () => {
    const user = userEvent.setup();
    renderDialog();

    const seedButton = screen.getByRole("button", { name: /^Seed$/ });
    expect(seedButton).toBeDisabled();

    await user.click(checkboxFor("Biomes"));
    expect(screen.getByRole("button", { name: /Seed 1 Target/ })).toBeEnabled();
  });

  it("selects and clears every target", async () => {
    const user = userEvent.setup();
    const { onCommit } = renderDialog();

    await user.click(screen.getByRole("button", { name: "Select all" }));
    await user.click(screen.getByRole("button", { name: /Seed 7 Targets/ }));

    expect(onCommit).toHaveBeenCalledWith(TARGETS.map((target) => target.target));

    await user.click(screen.getByRole("button", { name: "Clear" }));
    expect(screen.getByRole("button", { name: /^Seed$/ })).toBeDisabled();
  });
});

// ─── Catalogue state ──────────────────────────────────────────────────────────

describe("catalogue state", () => {
  it("says it is loading rather than looking like an empty catalogue", () => {
    renderDialog({ targets: [], loading: true });

    expect(screen.getByText(/Loading seed targets/)).toBeInTheDocument();
    expect(screen.queryByRole("checkbox")).not.toBeInTheDocument();
  });

  it("shows the error and offers no targets when the catalogue could not be loaded", () => {
    renderDialog({ targets: [], error: "Network error" });

    expect(screen.getByText("Network error")).toBeInTheDocument();
    expect(screen.queryByRole("checkbox")).not.toBeInTheDocument();
  });

  it("re-seeds to nothing each time it opens, so a previous choice does not leak into the next seed", async () => {
    const user = userEvent.setup();
    const onCommit = vi.fn();

    const { rerender } = render(
      <SeedSelectionDialog open targets={TARGETS} onCancel={vi.fn()} onCommit={onCommit} />,
    );

    await user.click(checkboxFor("Biomes"));
    expect(checkboxFor("Biomes").checked).toBe(true);

    rerender(<SeedSelectionDialog open={false} targets={TARGETS} onCancel={vi.fn()} onCommit={onCommit} />);
    rerender(<SeedSelectionDialog open targets={TARGETS} onCancel={vi.fn()} onCommit={onCommit} />);

    expect(checkboxFor("Biomes").checked).toBe(false);
  });
});
