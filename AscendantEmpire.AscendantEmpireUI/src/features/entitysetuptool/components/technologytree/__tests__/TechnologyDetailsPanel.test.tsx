import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { layOutTechnologyTree } from "../../../tools/technologyTreeLayout";
import TechnologyDetailsPanel from "../TechnologyDetailsPanel";
import { mixedCatalogue, needs, tech } from "./technologyFixtures";

// The panel is where the tree's two line styles are restated in words, and where catalogue damage is reported rather than tidied away. Those are the two
// things worth pinning here.
//
// Purely presentational: the parent owns the selection and the layout, so no network mocking and no react-query.

/** The laid-out node for one technology, which the panel needs alongside the catalogue record. */
function nodeFor(catalogue: ReturnType<typeof mixedCatalogue>, id: number) {
  return layOutTechnologyTree(catalogue).nodesById.get(id) ?? null;
}

function renderPanel(overrides: Partial<React.ComponentProps<typeof TechnologyDetailsPanel>> = {}) {
  const catalogue = mixedCatalogue();
  const onNavigate = vi.fn();
  const onToggleFullAncestry = vi.fn();

  render(
    <TechnologyDetailsPanel
      technology={catalogue.find((candidate) => candidate.id === 4) ?? null}
      node={nodeFor(catalogue, 4)}
      dependents={[{ id: 6, code: "T6", displayName: "Technology 6", kind: "Mandatory" }]}
      droppedPrerequisites={[]}
      showFullAncestry={false}
      onToggleFullAncestry={onToggleFullAncestry}
      onNavigate={onNavigate}
      {...overrides}
    />,
  );

  return { onNavigate, onToggleFullAncestry };
}

// ─── Nothing selected ───────────────────────────────────────────────────────────

describe("with nothing selected", () => {
  it("invites a selection rather than rendering an empty shell", () => {
    renderPanel({ technology: null, node: null });

    expect(screen.getByText(/Pick a technology/)).toBeInTheDocument();
  });
});

// ─── Prerequisites, split by kind ───────────────────────────────────────────────

describe("prerequisites", () => {
  it("splits them under headings that state the rule each kind encodes", () => {
    renderPanel();

    expect(screen.getByText(/All of — every one must be discovered/)).toBeInTheDocument();
    expect(screen.getByText(/Any one of — one is enough/)).toBeInTheDocument();
  });

  it("lists each prerequisite under the right heading", () => {
    renderPanel();

    // Technology 4 needs 2 and 3 outright, and either 1 or 5.
    const mandatory = screen.getByText(/All of/).parentElement;
    const optional = screen.getByText(/Any one of/).parentElement;

    expect(mandatory).toHaveTextContent("Technology 2");
    expect(mandatory).toHaveTextContent("Technology 3");
    expect(optional).toHaveTextContent("Technology 1");
    expect(optional).toHaveTextContent("Technology 5");
  });

  it("moves the selection when a prerequisite is clicked", async () => {
    const user = userEvent.setup();
    const { onNavigate } = renderPanel();

    await user.click(screen.getByRole("button", { name: /Technology 2/ }));

    expect(onNavigate).toHaveBeenCalledWith(2);
  });

  it("says a technology with no prerequisites is available immediately", () => {
    const catalogue = mixedCatalogue();

    renderPanel({ technology: catalogue[0], node: nodeFor(catalogue, 1), dependents: [] });

    expect(screen.getByText(/Available from the first turn/)).toBeInTheDocument();
  });

  it("reports a prerequisite that named something not in the catalogue", () => {
    renderPanel({ droppedPrerequisites: [{ prerequisiteTechnologyCode: "T99", reason: "missing" }] });

    expect(screen.getByText(/T99, which is not in the catalogue/)).toBeInTheDocument();
  });

  it("reports a technology that listed itself", () => {
    renderPanel({ droppedPrerequisites: [{ prerequisiteTechnologyCode: "T4", reason: "self" }] });

    expect(screen.getByText(/Lists itself/)).toBeInTheDocument();
  });
});

// ─── Dependents ─────────────────────────────────────────────────────────────────

describe("dependents", () => {
  it("separates the technologies that require this one from those it is merely an option for", () => {
    renderPanel({
      dependents: [
        { id: 6, code: "T6", displayName: "Technology 6", kind: "Mandatory" },
        { id: 7, code: "T7", displayName: "Technology 7", kind: "Optional" },
      ],
    });

    expect(screen.getByText("Requires this").parentElement).toHaveTextContent("Technology 6");
    expect(screen.getByText("Counts as one of its options").parentElement).toHaveTextContent("Technology 7");
  });

  it("says so when nothing depends on the technology", () => {
    renderPanel({ dependents: [] });

    expect(screen.getByText(/Nothing depends on this technology/)).toBeInTheDocument();
  });
});

// ─── Acquisition cost ───────────────────────────────────────────────────────────

describe("acquisition cost", () => {
  it("calls an empty cost free, rather than leaving the section blank", () => {
    renderPanel();

    expect(screen.getByText(/Free — no acquisition cost/)).toBeInTheDocument();
  });

  it("lists each Asset and how much of it is needed", () => {
    const catalogue = mixedCatalogue();
    const withCost = tech(4, { acquisitionCosts: [{ assetId: 1, assetCode: "timber", assetDisplayName: "Timber", assetIconPath: "", quantity: 2500 }] });

    renderPanel({ technology: withCost, node: nodeFor(catalogue, 4) });

    expect(screen.getByText(/2,500 × Timber/)).toBeInTheDocument();
  });
});

// ─── Grants, including the dangling ones ────────────────────────────────────────

describe("grants", () => {
  it("names the target and what discovering the technology does to it", () => {
    const catalogue = mixedCatalogue();
    const withGrant = tech(4, {
      grants: [{ targetType: "Asset", targetId: 7, targetCode: "timber", targetDisplayName: "Timber", grantEffect: "Unlock" }],
    });

    renderPanel({ technology: withGrant, node: nodeFor(catalogue, 4) });

    expect(screen.getByText("Unlock")).toBeInTheDocument();
    expect(screen.getByText("Timber")).toBeInTheDocument();
  });

  it("keeps a grant whose target has been deleted, and says what is wrong with it", () => {
    // A grant carries no foreign key, so it can outlive what it points at. Dropping the row, or printing "null", would both hide exactly the kind of damage
    // this tool exists to surface.
    const catalogue = mixedCatalogue();
    const dangling = tech(4, {
      grants: [{ targetType: "UnitComponent", targetId: 42, targetCode: null, targetDisplayName: null, grantEffect: "Unlock" }],
    });

    renderPanel({ technology: dangling, node: nodeFor(catalogue, 4) });

    expect(screen.getByText(/UnitComponent #42/)).toBeInTheDocument();
    expect(screen.getByText(/target no longer exists/)).toBeInTheDocument();
    expect(screen.queryByText(/null/)).not.toBeInTheDocument();
  });
});

// ─── Experience sources and the ancestry toggle ─────────────────────────────────

describe("experience sources", () => {
  it("lists the types that feed the technology", () => {
    const catalogue = mixedCatalogue();
    const withSources = tech(4, {
      experienceRequired: 250,
      experienceSources: [{ experienceTypeId: 1, experienceTypeCode: "hunting", experienceTypeDisplayName: "Hunting" }],
    });

    renderPanel({ technology: withSources, node: nodeFor(catalogue, 4) });

    expect(screen.getByText("Hunting")).toBeInTheDocument();
  });
});

describe("the full ancestry toggle", () => {
  it("reports the change rather than holding the state itself", async () => {
    const user = userEvent.setup();
    const { onToggleFullAncestry } = renderPanel();

    // Queried without a name: the shared ToggleSwitch puts role="switch" on a span, and a label's htmlFor only names a form control, so the switch has no
    // accessible name to match on. There is exactly one in this panel, so the bare role is unambiguous.
    await user.click(screen.getByRole("switch"));
    expect(screen.getByText("Show full ancestry")).toBeInTheDocument();

    expect(onToggleFullAncestry).toHaveBeenCalledWith(true);
  });
});

// ─── Layout diagnostics ─────────────────────────────────────────────────────────

describe("layout diagnostics", () => {
  it("explains when an optional route would reach the technology earlier than its column", () => {
    // Technology 4's deepest prerequisite is at column 1, but it also has an optional prerequisite at column 0, so the two readings differ.
    const catalogue = [
      tech(1),
      tech(2, { prerequisites: [needs(1)] }),
      tech(3, { prerequisites: [needs(2)] }),
      tech(4, { prerequisites: [needs(1, "Optional"), needs(3, "Optional")] }),
    ];
    const layout = layOutTechnologyTree(catalogue);

    render(
      <TechnologyDetailsPanel
        technology={catalogue[3]}
        node={layout.nodesById.get(4) ?? null}
        dependents={[]}
        droppedPrerequisites={[]}
        showFullAncestry={false}
        onToggleFullAncestry={vi.fn()}
        onNavigate={vi.fn()}
      />,
    );

    expect(screen.getByText(/An optional route reaches this as early as column 1/)).toBeInTheDocument();
  });
});
