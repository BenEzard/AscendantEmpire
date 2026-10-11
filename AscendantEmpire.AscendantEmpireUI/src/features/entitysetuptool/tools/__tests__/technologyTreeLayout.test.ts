import { describe, expect, it } from "vitest";
import type { CatalogueTechnology, CataloguePrerequisite, TechnologyPrerequisiteKind } from "../../api/technologyApi";
import { countCrossings, layOutTechnologyTree, truncateToWidth } from "../technologyTreeLayout";

// Layering, ordering, authored coordinates and the coordinate arithmetic. Pure input to pure output, so nothing here renders and nothing here mocks.
//
// Deliberately not tested: whether the result looks good, and the exact control-point values of the Bézier curves. The paths are only asserted on at their
// endpoints and on staying finite — the shape of the curve in between is a visual judgement, not a contract, and pinning it would make every tweak a test
// failure.

const codeFor = (id: number) => `T${id}`;

/** A prerequisite edge, named by the id it points at. Mandatory unless a test says otherwise. */
function needs(id: number, kind: TechnologyPrerequisiteKind = "Mandatory"): CataloguePrerequisite {
  return {
    prerequisiteTechnologyId: id,
    prerequisiteTechnologyCode: codeFor(id),
    prerequisiteTechnologyDisplayName: codeFor(id),
    prerequisiteKind: kind,
  };
}

/**
 * A catalogue technology with everything but its id and its prerequisites defaulted, so each test states only the edges it is actually about.
 *
 * Sort order is derived from the id, which keeps the initial ordering predictable — every tie in the layout breaks on sort order and then id.
 */
function tech(id: number, prerequisites: CataloguePrerequisite[] = [], overrides: Partial<CatalogueTechnology> = {}): CatalogueTechnology {
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
    ...overrides,
  };
}

/** The first and last coordinate pair of a cubic path built as "M x y C cx1 cy1 cx2 cy2 x y". */
function pathEndpoints(path: string): { startX: number; startY: number; endX: number; endY: number } {
  const tokens = path.split(" ");

  return {
    startX: Number(tokens[0].slice(1)),
    startY: Number(tokens[1]),
    endX: Number(tokens[6]),
    endY: Number(tokens[7]),
  };
}

/**
 * Every node's position on the canvas, so a test can assert no two boxes landed on top of each other, and compare one layout against another.
 *
 * Coordinates rather than column and row, because the unconnected band reuses the same column and row numbering from a different vertical origin — two nodes
 * sharing a cell there are nowhere near each other on screen.
 */
function occupiedPositions(nodes: readonly { x: number; y: number }[]): string[] {
  return nodes.map((node) => `${node.x},${node.y}`);
}

// ─── Layering ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

describe("layOutTechnologyTree layering", () => {
  it("places a technology one column right of its deepest prerequisite, not its shallowest", () => {
    // T4 can be reached from T1 in one hop, or in three via T2 and T3. Longest path puts it at column 3; shortest path would say 1.
    const layout = layOutTechnologyTree([
      tech(1),
      tech(2, [needs(1)]),
      tech(3, [needs(2)]),
      tech(4, [needs(1), needs(3)]),
    ]);

    expect(layout.nodesById.get(4)?.column).toBe(3);
    expect(layout.nodesById.get(4)?.depth).toBe(3);
  });

  it("gives a chain one column per link", () => {
    const chain = [tech(1), ...Array.from({ length: 7 }, (_unused, index) => tech(index + 2, [needs(index + 1)]))];
    const layout = layOutTechnologyTree(chain);

    expect(layout.layerCount).toBe(8);
    expect(chain.map((technology) => layout.nodesById.get(technology.id)?.column)).toEqual([0, 1, 2, 3, 4, 5, 6, 7]);
  });

  it("counts an optional prerequisite toward depth like any other, and reports the earlier any-of reading separately", () => {
    // T5's optional prerequisites sit at depths 1 and 3. Placing it by the deeper one keeps every edge pointing forwards; the shallower one is what a player
    // could actually reach it through, which is reported rather than drawn.
    const layout = layOutTechnologyTree([
      tech(1),
      tech(2, [needs(1)]),
      tech(3, [needs(2)]),
      tech(4, [needs(3)]),
      tech(5, [needs(2, "Optional"), needs(4, "Optional")]),
    ]);

    expect(layout.nodesById.get(5)?.column).toBe(4);
    expect(layout.nodesById.get(5)?.earliestDepth).toBe(2);
    expect(layout.edges.every((edge) => edge.span >= 1)).toBe(true);
  });

  it("keeps each edge's kind, so mandatory and optional stay distinguishable after layout", () => {
    const layout = layOutTechnologyTree([tech(1), tech(2), tech(3, [needs(1, "Mandatory"), needs(2, "Optional")])]);

    expect(layout.edges.find((edge) => edge.fromId === 1)?.kind).toBe("Mandatory");
    expect(layout.edges.find((edge) => edge.fromId === 2)?.kind).toBe("Optional");
  });
});

// ─── Damaged catalogues ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

describe("layOutTechnologyTree on a catalogue the upsert would have rejected", () => {
  it("lays out a prerequisite cycle without hanging, and leaves the rest of the tree alone", () => {
    // T1 needs T3, T3 needs T2, T2 needs T1 — nothing in that loop can ever reach indegree zero. T4 and T5 are a clean branch beside it.
    const layout = layOutTechnologyTree([
      tech(1, [needs(3)]),
      tech(2, [needs(1)]),
      tech(3, [needs(2)]),
      tech(4),
      tech(5, [needs(4)]),
    ]);

    expect(layout.nodes).toHaveLength(5);
    expect([...layout.cycleNodeIds].sort((first, second) => first - second)).toEqual([1, 2, 3]);

    // Non-finite coordinates are the realistic failure here, not a wrong number.
    expect(layout.nodes.every((node) => Number.isFinite(node.x) && Number.isFinite(node.y))).toBe(true);
    expect(layout.nodesById.get(4)?.column).toBe(0);
    expect(layout.nodesById.get(5)?.column).toBe(1);
    expect(layout.warnings.some((warning) => warning.includes("cycle"))).toBe(true);
  });

  it("drops a technology that lists itself as a prerequisite", () => {
    const layout = layOutTechnologyTree([tech(1, [needs(1)])]);

    expect(layout.droppedPrerequisites).toEqual([{ technologyId: 1, prerequisiteTechnologyCode: "T1", reason: "self" }]);
    expect(layout.edges).toHaveLength(0);
    expect(layout.nodesById.get(1)?.column).toBe(0);
  });

  it("drops a prerequisite pointing at a technology that is not in the catalogue, keeping its code for the panel", () => {
    const layout = layOutTechnologyTree([tech(1), tech(2, [needs(1), needs(99)])]);

    expect(layout.droppedPrerequisites).toEqual([{ technologyId: 2, prerequisiteTechnologyCode: "T99", reason: "missing" }]);

    // The dropped edge must not push T2 any further right than its one real prerequisite does.
    expect(layout.nodesById.get(2)?.column).toBe(1);
    expect(layout.edges).toHaveLength(1);
  });

  it("draws a prerequisite listed twice once, as mandatory, when the two rows disagree about the kind", () => {
    const layout = layOutTechnologyTree([tech(1), tech(2, [needs(1, "Optional"), needs(1, "Mandatory")])]);

    expect(layout.edges).toHaveLength(1);
    expect(layout.edges[0].kind).toBe("Mandatory");
    expect(layout.warnings.some((warning) => warning.includes("twice"))).toBe(true);
  });
});

// ─── Crossing reduction ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

describe("layOutTechnologyTree crossing reduction", () => {
  /** Two columns of six, wired in reverse, which is the worst case the sweeps can be handed. */
  function crossedCatalogue(): CatalogueTechnology[] {
    const roots = [1, 2, 3, 4, 5, 6].map((id) => tech(id));
    const leaves = [11, 12, 13, 14, 15, 16].map((id, index) => tech(id, [needs(6 - index)]));

    return [...roots, ...leaves];
  }

  it("reduces crossings below the unswept arrangement", () => {
    const swept = layOutTechnologyTree(crossedCatalogue());
    const unswept = layOutTechnologyTree(crossedCatalogue(), { crossingSweeps: 0 });

    expect(unswept.crossingCount).toBeGreaterThan(0);
    expect(swept.crossingCount).toBeLessThan(unswept.crossingCount);
  });

  it("reports the crossing count of the arrangement it actually returns", () => {
    const layout = layOutTechnologyTree(crossedCatalogue());
    const recounted = countCrossings(
      layout.nodes.map((node) => ({ id: node.id, column: node.column, row: node.row })),
      layout.edges,
    );

    expect(layout.crossingCount).toBe(recounted);
  });

  it("gives the same rows every time it runs", () => {
    const first = layOutTechnologyTree(crossedCatalogue());
    const second = layOutTechnologyTree(crossedCatalogue());

    expect(occupiedPositions(second.nodes)).toEqual(occupiedPositions(first.nodes));
  });

  it("ignores the order the technologies arrive in, so the tree does not reshuffle when the API reorders its rows", () => {
    const catalogue = crossedCatalogue();
    const shuffled = [...catalogue].reverse();

    const inOrder = layOutTechnologyTree(catalogue);
    const outOfOrder = layOutTechnologyTree(shuffled);

    for (const technology of catalogue) {
      expect(outOfOrder.nodesById.get(technology.id)?.column).toBe(inOrder.nodesById.get(technology.id)?.column);
      expect(outOfOrder.nodesById.get(technology.id)?.row).toBe(inOrder.nodesById.get(technology.id)?.row);
    }
  });
});

// ─── Authored coordinates ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

describe("layOutTechnologyTree authored coordinates", () => {
  it("uses an author's coordinates wholesale when every technology has them, and still reports the computed depth", () => {
    const layout = layOutTechnologyTree([
      tech(1, [], { treeColumn: 2, treeRow: 1 }),
      tech(2, [needs(1)], { treeColumn: 0, treeRow: 0 }),
    ]);

    expect(layout.nodesById.get(1)).toMatchObject({ column: 2, row: 1, placement: "authored" });
    expect(layout.nodesById.get(2)).toMatchObject({ column: 0, row: 0, placement: "authored" });

    // Depth is independent of where the box was put, so the panel can still say how deep in the tree the technology sits.
    expect(layout.nodesById.get(2)?.depth).toBe(1);
  });

  it("moves an authored technology into the column it was given, overriding its computed depth", () => {
    const layout = layOutTechnologyTree([tech(1), tech(2, [needs(1)], { treeColumn: 5, treeRow: 0 })]);

    expect(layout.nodesById.get(2)?.column).toBe(5);
    expect(layout.nodesById.get(2)?.depth).toBe(1);
  });

  it("pins an authored row and flows its unauthored siblings around it", () => {
    // T2, T3 and T4 all compute to column 1. T2 is pinned to row 2, so the other two take the rows it left free.
    const layout = layOutTechnologyTree([
      tech(1),
      tech(2, [needs(1)], { treeColumn: 1, treeRow: 2 }),
      tech(3, [needs(1)]),
      tech(4, [needs(1)]),
    ]);

    expect(layout.nodesById.get(2)?.row).toBe(2);
    expect([layout.nodesById.get(3)?.row, layout.nodesById.get(4)?.row].sort()).toEqual([0, 1]);
    expect(new Set(occupiedPositions(layout.nodes)).size).toBe(layout.nodes.length);
  });

  it("bumps the loser of an authored collision to the next free row, naming both technologies", () => {
    const layout = layOutTechnologyTree([
      tech(1),
      tech(2, [needs(1)], { treeColumn: 1, treeRow: 0 }),
      tech(3, [needs(1)], { treeColumn: 1, treeRow: 0 }),
    ]);

    // Sort order then id decides who keeps the cell, so T2 stays and T3 moves.
    expect(layout.nodesById.get(2)?.row).toBe(0);
    expect(layout.nodesById.get(3)?.row).toBe(1);
    expect(layout.warnings.some((warning) => warning.includes("T2") && warning.includes("T3"))).toBe(true);
  });

  it("ignores half a coordinate, laying the technology out from the graph and saying so", () => {
    const layout = layOutTechnologyTree([tech(1), tech(2, [needs(1)], { treeColumn: 5, treeRow: null })]);

    expect(layout.nodesById.get(2)).toMatchObject({ column: 1, placement: "layered" });
    expect(layout.warnings.some((warning) => warning.includes("T2"))).toBe(true);
  });

  it("clamps a negative authored column to zero", () => {
    const layout = layOutTechnologyTree([tech(1, [], { treeColumn: -3, treeRow: 0 })]);

    expect(layout.nodesById.get(1)?.column).toBe(0);
    expect(layout.warnings.some((warning) => warning.includes("negative"))).toBe(true);
  });
});

// ─── The unconnected band ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

describe("layOutTechnologyTree unconnected band", () => {
  it("lifts a technology with no edges in either direction out of the columns and into the band", () => {
    const layout = layOutTechnologyTree([tech(1), tech(2, [needs(1)]), tech(3)]);

    expect(layout.nodesById.get(3)).toMatchObject({ isOrphan: true, placement: "orphanBand" });
    expect(layout.orphanCount).toBe(1);

    // The band sits below everything the layered graph occupies.
    const layeredBottom = Math.max(
      ...layout.nodes.filter((node) => !node.isOrphan).map((node) => node.y + node.height),
    );

    expect(layout.orphanBandTop).not.toBeNull();
    expect(layout.orphanBandTop ?? 0).toBeGreaterThan(layeredBottom);
  });

  it("leaves the canvas no wider for the band than the graph already was", () => {
    const connectedOnly = layOutTechnologyTree([tech(1), tech(2, [needs(1)])]);
    const withAnOrphan = layOutTechnologyTree([tech(1), tech(2, [needs(1)]), tech(3)]);

    expect(withAnOrphan.width).toBe(connectedOnly.width);
  });

  it("has no band at all when nothing is unconnected", () => {
    const layout = layOutTechnologyTree([tech(1), tech(2, [needs(1)])]);

    expect(layout.orphanCount).toBe(0);
    expect(layout.orphanBandTop).toBeNull();

    // One row of boxes plus the padding either side — no band gap, and no height reserved for a band that does not exist.
    const tallestBox = Math.max(...layout.nodes.map((node) => node.y + node.height));

    expect(layout.height).toBe(tallestBox + 32);
  });

  it("still lays out a catalogue in which nothing is connected to anything", () => {
    const layout = layOutTechnologyTree([tech(1), tech(2), tech(3)]);

    expect(layout.layerCount).toBe(0);
    expect(layout.orphanCount).toBe(3);
    expect(layout.nodes.every((node) => Number.isFinite(node.x) && Number.isFinite(node.y))).toBe(true);
    expect(new Set(occupiedPositions(layout.nodes)).size).toBe(3);
  });
});

// ─── Coordinates and geometry ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

describe("layOutTechnologyTree geometry", () => {
  /** A small spread of shapes — a chain, a fan, a merge and a loose technology — so the invariants are asserted over more than one arrangement. */
  function mixedCatalogue(): CatalogueTechnology[] {
    return [
      tech(1),
      tech(2, [needs(1)]),
      tech(3, [needs(1)]),
      tech(4, [needs(2), needs(3)]),
      tech(5, [needs(4)]),
      tech(6, [needs(1, "Optional"), needs(5, "Optional")]),
      tech(7),
    ];
  }

  it("never puts two technologies in the same cell", () => {
    const layout = layOutTechnologyTree(mixedCatalogue());

    expect(new Set(occupiedPositions(layout.nodes)).size).toBe(layout.nodes.length);
  });

  it("keeps every box inside the canvas it reports", () => {
    const layout = layOutTechnologyTree(mixedCatalogue());

    for (const node of layout.nodes) {
      expect(node.x).toBeGreaterThanOrEqual(0);
      expect(node.y).toBeGreaterThanOrEqual(0);
      expect(node.x + node.width).toBeLessThanOrEqual(layout.width);
      expect(node.y + node.height).toBeLessThanOrEqual(layout.height);
    }
  });

  it("runs every edge from its source's right edge to its target's left edge, at mid height", () => {
    const layout = layOutTechnologyTree(mixedCatalogue());

    expect(layout.edges.length).toBeGreaterThan(0);

    for (const edge of layout.edges) {
      const from = layout.nodesById.get(edge.fromId);
      const to = layout.nodesById.get(edge.toId);
      const endpoints = pathEndpoints(edge.path);

      expect(endpoints.startX).toBe((from?.x ?? 0) + (from?.width ?? 0));
      expect(endpoints.startY).toBe((from?.y ?? 0) + (from?.height ?? 0) / 2);
      expect(endpoints.endX).toBe(to?.x);
      expect(endpoints.endY).toBe((to?.y ?? 0) + (to?.height ?? 0) / 2);
    }
  });

  it("indexes each technology's own edges, so a selection never has to scan them all", () => {
    const layout = layOutTechnologyTree(mixedCatalogue());

    expect(layout.incomingEdgeKeysById.get(4)).toEqual(["2->4", "3->4"]);
    expect(layout.outgoingEdgeKeysById.get(1)).toEqual(["1->2", "1->3", "1->6"]);
    expect(layout.prerequisiteIdsById.get(4)).toEqual([2, 3]);
    expect(layout.dependentIdsById.get(1)).toEqual([2, 3, 6]);
  });

  it("returns an empty canvas for an empty catalogue rather than throwing", () => {
    const layout = layOutTechnologyTree([]);

    expect(layout.nodes).toHaveLength(0);
    expect(layout.layerCount).toBe(0);
    expect(layout.width).toBe(64);
    expect(layout.height).toBe(64);
    expect(layout.orphanBandTop).toBeNull();
  });
});

// ─── Label truncation ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

describe("truncateToWidth", () => {
  it("leaves a name that fits untouched", () => {
    expect(truncateToWidth("Woodcutting", 160)).toBe("Woodcutting");
  });

  it("ellipsises a name that does not fit", () => {
    const truncated = truncateToWidth("Fundamentals of Military Service", 160);

    expect(truncated.endsWith("…")).toBe(true);
    expect(truncated.length).toBeLessThan("Fundamentals of Military Service".length);
  });

  it("keeps the full display name on the node even when the label is cut", () => {
    const longName = "Fundamentals of Military Service";
    const layout = layOutTechnologyTree([tech(1, [], { displayName: longName })]);

    expect(layout.nodesById.get(1)?.displayName).toBe(longName);
    expect(layout.nodesById.get(1)?.label.endsWith("…")).toBe(true);
  });
});
