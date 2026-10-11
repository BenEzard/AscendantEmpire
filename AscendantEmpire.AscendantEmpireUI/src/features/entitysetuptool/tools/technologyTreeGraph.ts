import type { TechnologyTreeLayout } from "./technologyTreeLayout";

// Graph traversal over a laid-out technology tree, and the highlight sets the canvas draws from. Pure, and kept apart from the layout module because none of
// it affects where a box goes — it is all about which boxes and edges are related to the one that is selected.

/**
 * How a node is drawn relative to the current selection.
 *
 * Declared here rather than on the node component because the builder below is what decides it, and a component importing a type from tools is a far better
 * dependency than tools importing one from a component.
 */
export type TechnologyNodeState = "base" | "selected" | "prerequisite" | "dependent" | "dimmed";

export interface TechnologyTreeHighlight {
  /** Only the nodes whose state is not "base" appear here, so an empty map means nothing is selected and everything draws normally. */
  nodeStates: ReadonlyMap<number, TechnologyNodeState>;
  emphasisedEdgeKeys: ReadonlySet<string>;
  /** True when something is selected, so the canvas knows to fade the edges that are not emphasised. */
  dimUnrelated: boolean;
}

/**
 * Walks an adjacency map outwards from one node and returns everything reachable, excluding the starting node itself.
 *
 * Iterative with a visited set, so a cycle terminates rather than recursing forever. The layout's cycle fallback means a damaged catalogue still produces
 * edges, and this has to survive them.
 */
function collectReachable(adjacencyById: ReadonlyMap<number, number[]>, startId: number): Set<number> {
  const reached = new Set<number>();
  const pending: number[] = [startId];

  // An index cursor rather than shift(), and the visited set checked before enqueueing, so each node is expanded at most once.
  for (let cursor = 0; cursor < pending.length; cursor += 1) {
    const neighbourIds = adjacencyById.get(pending[cursor]) ?? [];

    for (const neighbourId of neighbourIds) {
      if (neighbourId === startId || reached.has(neighbourId)) {
        continue;
      }

      reached.add(neighbourId);
      pending.push(neighbourId);
    }
  }

  return reached;
}

/** Every technology this one ultimately rests on, however many steps back. Excludes the technology itself. */
export function collectAncestors(layout: TechnologyTreeLayout, technologyId: number): Set<number> {
  return collectReachable(layout.prerequisiteIdsById, technologyId);
}

/** Every technology that ultimately depends on this one, however many steps forward. Excludes the technology itself. */
export function collectDescendants(layout: TechnologyTreeLayout, technologyId: number): Set<number> {
  return collectReachable(layout.dependentIdsById, technologyId);
}

/**
 * Works out which nodes and edges to emphasise for the current selection.
 *
 * With 210 edges on screen, following one by eye is the thing a reader genuinely cannot do, so a selection emphasises its own edges and fades the rest. The
 * default is one hop in each direction, which answers "what does this need, and what needs it"; the full-ancestry mode widens to the transitive closure both
 * ways, which answers "what does this ultimately rest on".
 */
export function buildTechnologyTreeHighlight(
  layout: TechnologyTreeLayout,
  selectedId: number | null,
  showFullAncestry: boolean,
): TechnologyTreeHighlight {
  if (selectedId === null || !layout.nodesById.has(selectedId)) {
    return { nodeStates: new Map(), emphasisedEdgeKeys: new Set(), dimUnrelated: false };
  }

  const prerequisiteIds = showFullAncestry
    ? collectAncestors(layout, selectedId)
    : new Set(layout.prerequisiteIdsById.get(selectedId) ?? []);

  const dependentIds = showFullAncestry
    ? collectDescendants(layout, selectedId)
    : new Set(layout.dependentIdsById.get(selectedId) ?? []);

  const nodeStates = new Map<number, TechnologyNodeState>();

  // Dim first, then paint the related nodes over the top, so a node that is both an ancestor and a descendant — possible only in a damaged, cyclic catalogue
  // — still comes out as one definite state rather than depending on iteration order.
  for (const node of layout.nodes) {
    nodeStates.set(node.id, "dimmed");
  }

  for (const prerequisiteId of prerequisiteIds) {
    nodeStates.set(prerequisiteId, "prerequisite");
  }

  for (const dependentId of dependentIds) {
    nodeStates.set(dependentId, "dependent");
  }

  nodeStates.set(selectedId, "selected");

  const emphasisedEdgeKeys = new Set<string>();

  if (showFullAncestry) {
    // Every edge inside the ancestry subgraph, in either direction, so the whole chain lights up rather than just the hops touching the selection.
    const ancestry = new Set<number>([selectedId, ...prerequisiteIds]);
    const descent = new Set<number>([selectedId, ...dependentIds]);

    for (const edge of layout.edges) {
      const insideAncestry = ancestry.has(edge.fromId) && ancestry.has(edge.toId);
      const insideDescent = descent.has(edge.fromId) && descent.has(edge.toId);

      if (insideAncestry || insideDescent) {
        emphasisedEdgeKeys.add(edge.key);
      }
    }
  } else {
    for (const key of layout.incomingEdgeKeysById.get(selectedId) ?? []) {
      emphasisedEdgeKeys.add(key);
    }

    for (const key of layout.outgoingEdgeKeysById.get(selectedId) ?? []) {
      emphasisedEdgeKeys.add(key);
    }
  }

  return { nodeStates, emphasisedEdgeKeys, dimUnrelated: true };
}
