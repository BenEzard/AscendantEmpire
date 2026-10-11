import { describe, expect, it } from "vitest";
import type { CatalogueTechnology, CataloguePrerequisite, TechnologyPrerequisiteKind } from "../../api/technologyApi";
import { buildTechnologyTreeHighlight, collectAncestors, collectDescendants } from "../technologyTreeGraph";
import { layOutTechnologyTree } from "../technologyTreeLayout";

// Traversal over a laid-out tree, and the highlight sets a selection produces. Nothing here renders; the layout is used as the input because that is what the
// real callers hold.

const codeFor = (id: number) => `T${id}`;

function needs(id: number, kind: TechnologyPrerequisiteKind = "Mandatory"): CataloguePrerequisite {
  return {
    prerequisiteTechnologyId: id,
    prerequisiteTechnologyCode: codeFor(id),
    prerequisiteTechnologyDisplayName: codeFor(id),
    prerequisiteKind: kind,
  };
}

function tech(id: number, prerequisites: CataloguePrerequisite[] = []): CatalogueTechnology {
  return {
    id,
    code: codeFor(id),
    displayName: codeFor(id),
    description: null,
    iconPath: "",
    era: "Stone",
    sortOrder: id * 10,
    treeColumn: null,
    treeRow: null,
    directedRequired: 100,
    experienceRequired: null,
    diffusionRequired: null,
    acquisitionCosts: [],
    prerequisites,
    grants: [],
    experienceSources: [],
  };
}

/** A chain with a diamond in the middle: 1 → 2, 1 → 3, 2 → 4, 3 → 4, 4 → 5. */
function diamond() {
  return layOutTechnologyTree([tech(1), tech(2, [needs(1)]), tech(3, [needs(1)]), tech(4, [needs(2), needs(3)]), tech(5, [needs(4)])]);
}

// ─── Traversal ──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

describe("collectAncestors", () => {
  it("reaches past the first hop to everything the technology ultimately rests on", () => {
    expect([...collectAncestors(diamond(), 5)].sort((first, second) => first - second)).toEqual([1, 2, 3, 4]);
  });

  it("excludes the technology it started from", () => {
    expect(collectAncestors(diamond(), 4).has(4)).toBe(false);
  });

  it("counts a technology reached by two routes once", () => {
    // T1 is a prerequisite of both T2 and T3, so a set that was not deduplicating would report it twice.
    expect([...collectAncestors(diamond(), 4)].sort((first, second) => first - second)).toEqual([1, 2, 3]);
  });

  it("returns nothing for a root", () => {
    expect(collectAncestors(diamond(), 1).size).toBe(0);
  });

  it("terminates on a prerequisite cycle rather than recursing forever", () => {
    const cyclic = layOutTechnologyTree([tech(1, [needs(3)]), tech(2, [needs(1)]), tech(3, [needs(2)])]);

    expect([...collectAncestors(cyclic, 1)].sort((first, second) => first - second)).toEqual([2, 3]);
  });
});

describe("collectDescendants", () => {
  it("reaches everything that ultimately depends on the technology", () => {
    expect([...collectDescendants(diamond(), 1)].sort((first, second) => first - second)).toEqual([2, 3, 4, 5]);
  });

  it("excludes the technology it started from, and returns nothing for a leaf", () => {
    expect(collectDescendants(diamond(), 1).has(1)).toBe(false);
    expect(collectDescendants(diamond(), 5).size).toBe(0);
  });
});

// ─── Highlighting ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

describe("buildTechnologyTreeHighlight", () => {
  it("leaves everything alone when nothing is selected", () => {
    const highlight = buildTechnologyTreeHighlight(diamond(), null, false);

    expect(highlight.nodeStates.size).toBe(0);
    expect(highlight.emphasisedEdgeKeys.size).toBe(0);
    expect(highlight.dimUnrelated).toBe(false);
  });

  it("leaves everything alone when the selected id is not in the catalogue", () => {
    expect(buildTechnologyTreeHighlight(diamond(), 99, false).dimUnrelated).toBe(false);
  });

  it("marks one hop in each direction and dims the rest", () => {
    const highlight = buildTechnologyTreeHighlight(diamond(), 4, false);

    expect(highlight.nodeStates.get(4)).toBe("selected");
    expect(highlight.nodeStates.get(2)).toBe("prerequisite");
    expect(highlight.nodeStates.get(3)).toBe("prerequisite");
    expect(highlight.nodeStates.get(5)).toBe("dependent");

    // T1 is two hops back, so by default it is not related to the selection.
    expect(highlight.nodeStates.get(1)).toBe("dimmed");
    expect(highlight.dimUnrelated).toBe(true);
  });

  it("emphasises only the selected technology's own edges by default", () => {
    const highlight = buildTechnologyTreeHighlight(diamond(), 4, false);

    expect([...highlight.emphasisedEdgeKeys].sort()).toEqual(["2->4", "3->4", "4->5"]);
  });

  it("extends to the whole ancestry, and to every edge within it, when full ancestry is on", () => {
    const highlight = buildTechnologyTreeHighlight(diamond(), 4, true);

    expect(highlight.nodeStates.get(1)).toBe("prerequisite");

    // Including 1->2 and 1->3, which touch no part of the selection directly but are links in the chain it rests on.
    expect([...highlight.emphasisedEdgeKeys].sort()).toEqual(["1->2", "1->3", "2->4", "3->4", "4->5"]);
  });
});
