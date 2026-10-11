import { render, screen } from "@testing-library/react";

import TechnologyTreeLegend from "../TechnologyTreeLegend";

// The legend is the one place the two line styles are explained in words rather than shown, so it is what a reader who cannot tell a dashed stroke from a
// solid one at low zoom relies on. The rest of it is the catalogue's problems, each of which must stay hidden while there is nothing to report.

function renderLegend(overrides: Partial<React.ComponentProps<typeof TechnologyTreeLegend>> = {}) {
  render(
    <TechnologyTreeLegend optionalEdgeCount={21} orphanCount={0} cycleNodeCount={0} droppedPrerequisiteCount={0} warnings={[]} {...overrides} />,
  );
}

// ─── The line styles ────────────────────────────────────────────────────────────

describe("the line styles", () => {
  it("describes both in words, with the rule each one encodes attached", () => {
    renderLegend();

    expect(screen.getByText(/Mandatory: every one must be discovered/)).toBeInTheDocument();
    expect(screen.getByText(/Optional: any one of them is enough/)).toBeInTheDocument();
  });

  it("names the styles themselves, so the words and the drawing can be matched up", () => {
    renderLegend();

    expect(screen.getByText("Solid")).toBeInTheDocument();
    expect(screen.getByText("Dotted")).toBeInTheDocument();
  });
});

// ─── Problem rows ───────────────────────────────────────────────────────────────

describe("with a clean catalogue", () => {
  it("shows nothing but the two line styles", () => {
    renderLegend();

    expect(screen.queryByText(/no prerequisites and no dependents/)).not.toBeInTheDocument();
    expect(screen.queryByText(/prerequisite cycle/)).not.toBeInTheDocument();
    expect(screen.queryByText(/could not be resolved/)).not.toBeInTheDocument();
  });
});

describe("with problems to report", () => {
  it("counts the unconnected technologies", () => {
    renderLegend({ orphanCount: 42 });

    expect(screen.getByText(/42 technologies have no prerequisites and no dependents/)).toBeInTheDocument();
  });

  it("counts the technologies on a cycle", () => {
    renderLegend({ cycleNodeCount: 3 });

    expect(screen.getByText(/3 technologies sit on a prerequisite cycle/)).toBeInTheDocument();
  });

  it("counts the prerequisites that could not be drawn", () => {
    renderLegend({ droppedPrerequisiteCount: 1 });

    expect(screen.getByText(/1 prerequisite could not be resolved/)).toBeInTheDocument();
  });

  it("lists the layout's own warnings, and caps the list rather than growing without limit", () => {
    renderLegend({ warnings: ["first", "second", "third", "fourth", "fifth"] });

    expect(screen.getByText("first")).toBeInTheDocument();
    expect(screen.getByText("third")).toBeInTheDocument();
    expect(screen.queryByText("fourth")).not.toBeInTheDocument();
    expect(screen.getByText(/and 2 more/)).toBeInTheDocument();
  });
});
