import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { layOutTechnologyTree } from "../../../tools/technologyTreeLayout";
import TechnologyTreeOutline from "../TechnologyTreeOutline";
import { mixedCatalogue, tech } from "./technologyFixtures";

// The rail is the graph's non-visual representation and the only keyboard route into it, so what is pinned here is that every technology is reachable by
// name, grouped the way the canvas lays it out, and that the selection is announced.

function renderOutline(overrides: Partial<React.ComponentProps<typeof TechnologyTreeOutline>> = {}) {
  const onSelect = vi.fn();
  const onFilterTextChange = vi.fn();

  render(
    <TechnologyTreeOutline
      layout={layOutTechnologyTree(mixedCatalogue())}
      selectedId={null}
      onSelect={onSelect}
      filterText=""
      onFilterTextChange={onFilterTextChange}
      {...overrides}
    />,
  );

  return { onSelect, onFilterTextChange };
}

// ─── Grouping ───────────────────────────────────────────────────────────────────

describe("grouping", () => {
  it("groups technologies by the column they are drawn in, with a count per group", () => {
    renderOutline();

    // The fixture is one root, three technologies at column 1, then Technology 4 and Technology 6 behind them.
    expect(screen.getByRole("heading", { name: /Column 0 — 1 technology/ })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: /Column 1 — 3 technologies/ })).toBeInTheDocument();
  });

  it("labels each row with its prerequisite and dependent counts, compactly", () => {
    renderOutline();

    expect(screen.getByText("Technology 1 (0 / 4)")).toBeInTheDocument();
    expect(screen.getByText("Technology 4 (4 / 1)")).toBeInTheDocument();
  });

  it("explains what the two numbers are, so the compact form is not cryptic", () => {
    renderOutline();

    expect(screen.getByText(/name \(prerequisites \/ dependents\)/)).toBeInTheDocument();
  });

  it("opens the counts out in words for the accessible name, starting with the visible label", () => {
    // "(4 / 1)" read aloud is noise, and an accessible name that dropped the visible label would be heard as a different control.
    renderOutline();

    expect(screen.getByRole("button", { name: "Technology 1 (0 / 4) — 0 prerequisites, 4 dependents" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Technology 4 (4 / 1) — 4 prerequisites, 1 dependent" })).toBeInTheDocument();
  });

  it("gives unconnected technologies a heading of their own, matching the band they are drawn in", () => {
    renderOutline({ layout: layOutTechnologyTree([...mixedCatalogue(), tech(99)]) });

    expect(screen.getByRole("heading", { name: /Unconnected — 1 technology/ })).toBeInTheDocument();
  });

  it("has no unconnected heading when every technology is wired into the tree", () => {
    renderOutline();

    expect(screen.queryByRole("heading", { name: /Unconnected/ })).not.toBeInTheDocument();
  });
});

// ─── Filtering ──────────────────────────────────────────────────────────────────

describe("filtering", () => {
  it("narrows the list to what matches, and says how many of the whole are showing", () => {
    renderOutline({ filterText: "Technology 4" });

    expect(screen.getByRole("button", { name: /Technology 4/ })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Technology 2/ })).not.toBeInTheDocument();
    expect(screen.getByText(/1 of 6/)).toBeInTheDocument();
  });

  it("matches on the code as well as the display name", () => {
    renderOutline({ filterText: "t3" });

    expect(screen.getByRole("button", { name: /Technology 3/ })).toBeInTheDocument();
  });

  it("says so when nothing matches, rather than showing an empty rail", () => {
    renderOutline({ filterText: "nothing like this" });

    expect(screen.getByText(/Nothing matches that/)).toBeInTheDocument();
  });

  it("reports what was typed rather than holding the text itself", async () => {
    const user = userEvent.setup();
    const { onFilterTextChange } = renderOutline();

    await user.type(screen.getByRole("searchbox", { name: "Filter technologies" }), "x");

    expect(onFilterTextChange).toHaveBeenCalledWith("x");
  });
});

// ─── Selection ──────────────────────────────────────────────────────────────────

describe("selection", () => {
  it("announces the selected row to assistive technology, not only by colour", () => {
    renderOutline({ selectedId: 4 });

    expect(screen.getByRole("button", { name: /Technology 4/ })).toHaveAttribute("aria-current", "true");
    expect(screen.getByRole("button", { name: /Technology 2/ })).not.toHaveAttribute("aria-current");
  });

  it("reports the clicked technology's id", async () => {
    const user = userEvent.setup();
    const { onSelect } = renderOutline();

    await user.click(screen.getByRole("button", { name: /Technology 3/ }));

    expect(onSelect).toHaveBeenCalledWith(3);
  });
});
