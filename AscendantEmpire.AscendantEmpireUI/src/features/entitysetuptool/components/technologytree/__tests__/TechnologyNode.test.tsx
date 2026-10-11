import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { layOutTechnologyTree } from "../../../tools/technologyTreeLayout";
import TechnologyNode from "../TechnologyNode";
import { tech } from "./technologyFixtures";

// One box. Rendered inside an svg wrapper, because an SVG child outside one does not lay out or hit-test.
//
// Deliberately not tested: the hover appearance, which is pure CSS with no state behind it, and the exact pixel geometry, which the layout module owns.

/** A laid-out node for a technology, which is what the component takes. */
function nodeFor(technology = tech(1)) {
  return layOutTechnologyTree([technology]).nodesById.get(technology.id)!;
}

function renderNode(overrides: Partial<React.ComponentProps<typeof TechnologyNode>> = {}) {
  const onSelect = vi.fn();

  const { container } = render(
    <svg>
      <TechnologyNode node={nodeFor()} state="base" onSelect={onSelect} {...overrides} />
    </svg>,
  );

  return { onSelect, container };
}

// ─── What the box says ──────────────────────────────────────────────────────────

describe("the box", () => {
  it("carries the full display name and the edge counts as its tooltip, even when the label is truncated", () => {
    const longName = "Fundamentals of Military Service";

    const { container } = renderNode({ node: nodeFor(tech(1, { displayName: longName })) });

    // Queried directly rather than with getByTitle, which only matches a title element that is an immediate child of the svg — this one belongs to the
    // node's own group. The visible label is cut to fit the box; the title is where the whole name lives.
    expect(container.querySelector("title")?.textContent).toBe(`${longName} — 0 prerequisites, 0 dependents`);
  });

  it("shows the technology's code", () => {
    renderNode();

    expect(screen.getByText("T1")).toBeInTheDocument();
  });

  it("marks one dot per research bucket in use, and none for a bucket that is unused", () => {
    const { container } = render(
      <svg>
        <TechnologyNode node={nodeFor(tech(1, { directedRequired: 100, experienceRequired: 50, diffusionRequired: null }))} state="base" onSelect={vi.fn()} />
      </svg>,
    );

    expect(container.querySelector("[data-bucket='directed']")).not.toBeNull();
    expect(container.querySelector("[data-bucket='experience']")).not.toBeNull();
    expect(container.querySelector("[data-bucket='diffusion']")).toBeNull();
  });
});

// ─── State ──────────────────────────────────────────────────────────────────────

describe("state", () => {
  it("reports the state it was given, so the canvas's highlighting is inspectable", () => {
    renderNode({ state: "prerequisite" });

    expect(screen.getByTestId("technology-node-1")).toHaveAttribute("data-state", "prerequisite");
  });

  it("marks the selected box with a corner notch, so selection is not carried by colour alone", () => {
    renderNode({ state: "selected" });

    expect(screen.getByTestId("technology-node-notch-1")).toBeInTheDocument();
  });

  it("gives an unselected box no notch", () => {
    renderNode({ state: "base" });

    expect(screen.queryByTestId("technology-node-notch-1")).not.toBeInTheDocument();
  });
});

// ─── Selection ──────────────────────────────────────────────────────────────────

describe("clicking", () => {
  it("reports the technology's id and holds no state of its own", async () => {
    const user = userEvent.setup();
    const { onSelect } = renderNode();

    await user.click(screen.getByTestId("technology-node-1"));

    expect(onSelect).toHaveBeenCalledWith(1);
  });
});
