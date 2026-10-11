import type { CatalogueTechnology, TechnologyPrerequisiteKind } from "../api/technologyApi";

// Lays the technology catalogue out as a left-to-right layered graph: column is depth in the prerequisite tree, row is position within that column.
//
// Pure — no React, no DOM, no measurement. Everything the canvas needs is computed here, including each edge's SVG path string and each node's truncated
// label, following the same division of labour as RiverStrokeLayer: the caller works out the geometry, the SVG component renders a plain array of it. That is
// what makes the hard part of this feature testable without rendering anything, and what keeps the render path free of per-frame arithmetic.

/** How wide a node box is. Fits roughly 22 characters of the 13px label, which is two-thirds of the catalogue's display names whole. */
const NODE_WIDTH = 176;

/** How tall a node box is. Two text lines — the display name, then the code and the bucket dots — at 13px and 10px. */
const NODE_HEIGHT = 48;

/** Horizontal space between one column's boxes and the next. Enough run for an edge to leave and enter horizontally and still read as a curve. */
const LAYER_GAP = 96;

/** Vertical space between one row's boxes and the next. Gives a row pitch visibly larger than the stroke weights crossing it. */
const ROW_GAP = 16;

/** Space between the outermost boxes and the edge of the canvas, so nothing is flush against the viewBox at fit zoom. */
const CANVAS_PADDING = 32;

/** Space between the bottom of the layered graph and the top of the unconnected band. Wider than the row pitch, so the band reads as a separate region. */
const ORPHAN_BAND_GAP = 80;

/** How many alternating median sweeps to run when reducing edge crossings. */
const CROSSING_SWEEPS = 8;

/**
 * Average rendered width of one label character, in pixels, at the node label's 13px size.
 *
 * An estimate, not a measurement: measuring text means a DOM, and a DOM is exactly what this module does without. Erring slightly wide means a label is
 * occasionally truncated a character earlier than it had to be, which is invisible; erring narrow would let text spill past the box, which is not.
 */
const LABEL_CHAR_WIDTH_PX = 7.2;

/** Horizontal padding inside a node box, each side, which is what the label has to fit between. */
const NODE_LABEL_PADDING = 8;

export interface TechnologyTreeLayoutOptions {
  nodeWidth?: number;
  nodeHeight?: number;
  layerGap?: number;
  rowGap?: number;
  padding?: number;
  orphanBandGap?: number;
  /** How many median sweeps to run. Zero disables crossing reduction entirely, which is how a test measures what the sweeps are worth. */
  crossingSweeps?: number;
}

/** How a node came by its position: computed from the graph, taken from an author's coordinates, or tiled into the unconnected band. */
export type NodePlacement = "layered" | "authored" | "orphanBand";

/** Why a prerequisite named in the catalogue produced no edge. */
export type DroppedPrerequisiteReason = "missing" | "self";

export interface LaidOutTechnologyNode {
  id: number;
  code: string;
  displayName: string;
  /** The display name truncated to fit the box. The full text stays on displayName, for the tooltip and the outline. */
  label: string;
  column: number;
  row: number;
  x: number;
  y: number;
  width: number;
  height: number;
  /** Longest-path depth over every resolved prerequisite, mandatory and optional alike. This is what the column is. */
  depth: number;
  /**
   * The depth this technology would sit at if each of its optional prerequisites were satisfied by whichever of them is shallowest.
   *
   * Always less than or equal to depth, and reported rather than used: it is the semantically correct "earliest a player could reach this" reading, but
   * placing a box by it would put it level with or left of some of its own optional prerequisites. See the note on layering below.
   */
  earliestDepth: number;
  prerequisiteCount: number;
  dependentCount: number;
  /**
   * Which of the three research buckets this technology uses at all.
   *
   * Carried here, next to the truncated label, because the node box marks them with dots and threading the catalogue through two memo boundaries for three
   * booleans would cost more than it saves. How *much* each bucket needs is a panel question, and stays on the catalogue record.
   */
  usesDirected: boolean;
  usesExperience: boolean;
  usesDiffusion: boolean;
  /** No resolved prerequisites and no resolved dependents. Those nodes leave the columns for the unconnected band. */
  isOrphan: boolean;
  /** On, or downstream of, a prerequisite cycle. The upsert forbids cycles, so this should never be true against real data. */
  isInCycle: boolean;
  placement: NodePlacement;
}

export interface LaidOutTechnologyEdge {
  /** Unique per edge, and the handle the highlight sets use. */
  key: string;
  /** The prerequisite — the technology the edge comes out of. */
  fromId: number;
  /** The technology that needs the prerequisite — the technology the edge points at. */
  toId: number;
  kind: TechnologyPrerequisiteKind;
  /** Column distance, target minus source. At least 1 for every edge in a clean, unauthored catalogue. */
  span: number;
  /** The SVG path d attribute, precomputed so no arithmetic happens during a render. */
  path: string;
}

export interface DroppedPrerequisite {
  technologyId: number;
  prerequisiteTechnologyCode: string;
  reason: DroppedPrerequisiteReason;
}

export interface TechnologyTreeLayout {
  nodes: LaidOutTechnologyNode[];
  nodesById: ReadonlyMap<number, LaidOutTechnologyNode>;
  edges: LaidOutTechnologyEdge[];
  /** Keys of the edges pointing *at* each node — its prerequisite edges. Lets a selection find its own edges without rescanning them all. */
  incomingEdgeKeysById: ReadonlyMap<number, string[]>;
  /** Keys of the edges leaving each node — the edges of the technologies that need it. */
  outgoingEdgeKeysById: ReadonlyMap<number, string[]>;
  prerequisiteIdsById: ReadonlyMap<number, number[]>;
  dependentIdsById: ReadonlyMap<number, number[]>;
  width: number;
  height: number;
  /** How many layered columns there are. Zero for a catalogue with nothing but unconnected technologies, or nothing at all. */
  layerCount: number;
  /** How many nodes sit in each layered column, indexed by column. */
  layerSizes: number[];
  /** The y the unconnected band starts at, or null when there are no unconnected technologies — in which case the band does not exist at all. */
  orphanBandTop: number | null;
  orphanCount: number;
  cycleNodeIds: number[];
  droppedPrerequisites: DroppedPrerequisite[];
  /** Authoring problems worth telling whoever is looking at the tree about. Rendered as rows in the legend card. */
  warnings: string[];
  /** Edge crossings in the arrangement being returned. Reported so a test can assert the sweeps actually reduced them. */
  crossingCount: number;
}

/** A node as the crossing counter needs it. */
interface PositionedNode {
  id: number;
  column: number;
  row: number;
}

/** A directed edge as the crossing counter needs it. */
interface DirectedEdge {
  fromId: number;
  toId: number;
}

/** Everything the layout works out about one technology before it has coordinates. */
interface WorkingNode {
  technology: CatalogueTechnology;
  id: number;
  sortOrder: number;
  /** Ids of the technologies this one needs, resolved — a prerequisite that was dropped is not in here. */
  prerequisiteIds: number[];
  /** Ids of the technologies that need this one. */
  dependentIds: number[];
  depth: number;
  earliestDepth: number;
  isInCycle: boolean;
  isOrphan: boolean;
  isAuthored: boolean;
  column: number;
  row: number;
  placement: NodePlacement;
}

/**
 * Cuts a display name down to what fits inside a node box, adding an ellipsis when it had to.
 *
 * Exported for its own test, and because the outline rail wants the untruncated name next to the same boxes.
 */
export function truncateToWidth(text: string, maxPixels: number, charWidthPx: number = LABEL_CHAR_WIDTH_PX): string {
  const maxCharacters = Math.floor(maxPixels / charWidthPx);

  if (maxCharacters <= 0) {
    return "";
  }

  if (text.length <= maxCharacters) {
    return text;
  }

  // One character of the budget goes to the ellipsis itself, so the result never comes out wider than the untruncated case would have been allowed to be.
  if (maxCharacters === 1) {
    return "…";
  }

  return `${text.slice(0, maxCharacters - 1).trimEnd()}…`;
}

/**
 * Counts how many pairs of edges cross each other.
 *
 * An edge is treated as a straight line from its source's row to its target's row, and is counted in every column gap it spans. Long edges are therefore
 * approximated — there are no dummy nodes to give them an intermediate row — which is fine, because this number is a score to compare arrangements by, not a
 * figure anyone reads.
 *
 * Exported so the crossing-reduction test can assert against it directly rather than through the sweeps.
 */
export function countCrossings(nodes: readonly PositionedNode[], edges: readonly DirectedEdge[]): number {
  const positionById = new Map<number, PositionedNode>();

  for (const node of nodes) {
    positionById.set(node.id, node);
  }

  // Bucket each edge into every gap it crosses, as the pair of rows it enters and leaves that gap at.
  const pairsByGap = new Map<number, { source: number; target: number }[]>();

  for (const edge of edges) {
    const from = positionById.get(edge.fromId);
    const to = positionById.get(edge.toId);

    if (from === undefined || to === undefined) {
      continue;
    }

    for (let gap = from.column; gap < to.column; gap += 1) {
      let pairs = pairsByGap.get(gap);

      if (pairs === undefined) {
        pairs = [];
        pairsByGap.set(gap, pairs);
      }

      pairs.push({ source: from.row, target: to.row });
    }
  }

  let crossings = 0;

  // Brute-force inversion count per gap. The widest gap in the real catalogue carries about 70 edges, so this is a few thousand comparisons — far cheaper
  // than the merge-sort accumulator it would take to beat it.
  for (const pairs of pairsByGap.values()) {
    for (let first = 0; first < pairs.length; first += 1) {
      for (let second = first + 1; second < pairs.length; second += 1) {
        const sourceDelta = pairs[first].source - pairs[second].source;
        const targetDelta = pairs[first].target - pairs[second].target;

        if (sourceDelta * targetDelta < 0) {
          crossings += 1;
        }
      }
    }
  }

  return crossings;
}

/** The median of a list of rows. Used rather than the mean because one far-away neighbour should not drag a node across its whole column. */
function medianOf(values: readonly number[]): number {
  const sorted = [...values].sort((first, second) => first - second);
  const middle = Math.floor(sorted.length / 2);

  if (sorted.length % 2 === 1) {
    return sorted[middle];
  }

  return (sorted[middle - 1] + sorted[middle]) / 2;
}

/**
 * Lays the technology catalogue out for the tree view.
 *
 * The whole layout is a pure function of the catalogue and the options: it does not depend on the order the technologies arrive in, so the view does not
 * reshuffle itself when the API changes its row order. Every ordering decision breaks ties on sort order and then id.
 */
export function layOutTechnologyTree(
  technologies: readonly CatalogueTechnology[],
  options?: TechnologyTreeLayoutOptions,
): TechnologyTreeLayout {
  const nodeWidth = options?.nodeWidth ?? NODE_WIDTH;
  const nodeHeight = options?.nodeHeight ?? NODE_HEIGHT;
  const layerGap = options?.layerGap ?? LAYER_GAP;
  const rowGap = options?.rowGap ?? ROW_GAP;
  const padding = options?.padding ?? CANVAS_PADDING;
  const orphanBandGap = options?.orphanBandGap ?? ORPHAN_BAND_GAP;
  const crossingSweeps = options?.crossingSweeps ?? CROSSING_SWEEPS;

  const columnPitch = nodeWidth + layerGap;
  const rowPitch = nodeHeight + rowGap;
  const labelWidth = nodeWidth - NODE_LABEL_PADDING * 2;

  const warnings: string[] = [];
  const droppedPrerequisites: DroppedPrerequisite[] = [];

  // ── Index, in a deterministic order ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
  //
  // Sorting up front is what makes the whole function independent of the input array's order: every later pass walks this array or something derived from it.

  const technologyById = new Map<number, CatalogueTechnology>();

  for (const technology of technologies) {
    technologyById.set(technology.id, technology);
  }

  const orderedTechnologies = [...technologies].sort(
    (first, second) => first.sortOrder - second.sortOrder || first.id - second.id,
  );

  const workingNodes: WorkingNode[] = orderedTechnologies.map((technology) => ({
    technology,
    id: technology.id,
    sortOrder: technology.sortOrder,
    prerequisiteIds: [],
    dependentIds: [],
    depth: 0,
    earliestDepth: 0,
    isInCycle: false,
    isOrphan: false,
    isAuthored: technology.treeColumn !== null && technology.treeRow !== null,
    column: 0,
    row: 0,
    placement: "layered",
  }));

  const workingById = new Map<number, WorkingNode>();

  for (const node of workingNodes) {
    workingById.set(node.id, node);
  }

  if (workingNodes.length === 0) {
    // Nothing to lay out. Returning an empty canvas rather than throwing matters: the viewer renders this while the catalogue is still loading.
    return {
      nodes: [],
      nodesById: new Map(),
      edges: [],
      incomingEdgeKeysById: new Map(),
      outgoingEdgeKeysById: new Map(),
      prerequisiteIdsById: new Map(),
      dependentIdsById: new Map(),
      width: padding * 2,
      height: padding * 2,
      layerCount: 0,
      layerSizes: [],
      orphanBandTop: null,
      orphanCount: 0,
      cycleNodeIds: [],
      droppedPrerequisites: [],
      warnings: [],
      crossingCount: 0,
    };
  }

  // ── Resolve the edges ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
  //
  // A prerequisite row can name something that is not there to point at. The upsert rejects a self-prerequisite and an unresolvable code, so neither should
  // reach here — but a catalogue the viewer cannot draw is precisely what the viewer is for, so each is dropped, recorded, and reported in the panel.

  interface ResolvedEdge {
    fromId: number;
    toId: number;
    kind: TechnologyPrerequisiteKind;
  }

  const resolvedEdges: ResolvedEdge[] = [];
  const seenEdgeKeys = new Map<string, ResolvedEdge>();

  for (const node of workingNodes) {
    // Sort each technology's prerequisites the same way as everything else, so the edge array — and with it every median and every tie-break — is
    // independent of the order the API happened to list them in.
    const orderedPrerequisites = [...node.technology.prerequisites].sort((first, second) => {
      const firstNode = workingById.get(first.prerequisiteTechnologyId);
      const secondNode = workingById.get(second.prerequisiteTechnologyId);
      const firstSortOrder = firstNode?.sortOrder ?? Number.MAX_SAFE_INTEGER;
      const secondSortOrder = secondNode?.sortOrder ?? Number.MAX_SAFE_INTEGER;

      return firstSortOrder - secondSortOrder || first.prerequisiteTechnologyId - second.prerequisiteTechnologyId;
    });

    for (const prerequisite of orderedPrerequisites) {
      if (prerequisite.prerequisiteTechnologyId === node.id) {
        droppedPrerequisites.push({
          technologyId: node.id,
          prerequisiteTechnologyCode: prerequisite.prerequisiteTechnologyCode,
          reason: "self",
        });
        continue;
      }

      if (!technologyById.has(prerequisite.prerequisiteTechnologyId)) {
        droppedPrerequisites.push({
          technologyId: node.id,
          prerequisiteTechnologyCode: prerequisite.prerequisiteTechnologyCode,
          reason: "missing",
        });
        continue;
      }

      const key = `${prerequisite.prerequisiteTechnologyId}->${node.id}`;
      const existing = seenEdgeKeys.get(key);

      if (existing !== undefined) {
        // A duplicated row would otherwise draw two paths on top of each other. Keep the first, and prefer Mandatory if the two disagree, because the
        // stricter reading is the safer thing to show.
        if (existing.kind !== prerequisite.prerequisiteKind) {
          existing.kind = "Mandatory";
          warnings.push(
            `${node.technology.code} lists ${prerequisite.prerequisiteTechnologyCode} as a prerequisite twice, once mandatory and once optional. Shown as mandatory.`,
          );
        }
        continue;
      }

      const edge: ResolvedEdge = { fromId: prerequisite.prerequisiteTechnologyId, toId: node.id, kind: prerequisite.prerequisiteKind };

      seenEdgeKeys.set(key, edge);
      resolvedEdges.push(edge);
      node.prerequisiteIds.push(prerequisite.prerequisiteTechnologyId);
      workingById.get(prerequisite.prerequisiteTechnologyId)?.dependentIds.push(node.id);
    }
  }

  // ── Layering: longest path over every edge ──────────────────────────────────────────────────────────────────────────────────────────────────────────────
  //
  // A node sits one column right of its *deepest* prerequisite, counting optional edges exactly like mandatory ones.
  //
  // The semantically correct "earliest availability" reading would take the *shallowest* of the optional group, since any one of them is enough. That is
  // rejected for placement because it is the only rule that breaks monotonicity: a node placed by its shallowest optional prerequisite can land level with,
  // or left of, its deeper optional siblings, and an edge running right-to-left in a left-to-right tree reads as a cycle. Taking the max guarantees every
  // edge spans at least one column, which is what makes "column is how deep in the tree" true and keeps edge routing to a single case. The other reading is
  // kept as earliestDepth and stated in the details panel in words, where a nuance belongs.

  const indegreeById = new Map<number, number>();

  for (const node of workingNodes) {
    indegreeById.set(node.id, node.prerequisiteIds.length);
  }

  // Kahn's algorithm, relaxing depth as it goes. An index cursor rather than shift(), and a queue bounded by nodes plus edges, so this cannot loop however
  // damaged the graph is.
  const queue: number[] = workingNodes.filter((node) => node.prerequisiteIds.length === 0).map((node) => node.id);
  const placedIds = new Set<number>(queue);

  for (let cursor = 0; cursor < queue.length; cursor += 1) {
    const current = workingById.get(queue[cursor]);

    if (current === undefined) {
      continue;
    }

    for (const dependentId of current.dependentIds) {
      const dependent = workingById.get(dependentId);

      if (dependent === undefined) {
        continue;
      }

      dependent.depth = Math.max(dependent.depth, current.depth + 1);

      const remaining = (indegreeById.get(dependentId) ?? 0) - 1;
      indegreeById.set(dependentId, remaining);

      if (remaining === 0) {
        placedIds.add(dependentId);
        queue.push(dependentId);
      }
    }
  }

  // Anything never enqueued is on a prerequisite cycle, or downstream of one. The upsert enforces acyclicity so this should be unreachable; it exists so a
  // damaged catalogue renders imperfectly instead of hanging the tab. One pass, using only the depths that did get settled, so it terminates by construction.
  const cycleNodeIds: number[] = [];

  for (const node of workingNodes) {
    if (placedIds.has(node.id)) {
      continue;
    }

    let deepestPlaced = -1;

    for (const prerequisiteId of node.prerequisiteIds) {
      if (!placedIds.has(prerequisiteId)) {
        continue;
      }

      deepestPlaced = Math.max(deepestPlaced, workingById.get(prerequisiteId)?.depth ?? -1);
    }

    node.depth = deepestPlaced + 1;
    node.isInCycle = true;
    cycleNodeIds.push(node.id);
  }

  if (cycleNodeIds.length > 0) {
    warnings.push(
      `${cycleNodeIds.length} ${cycleNodeIds.length === 1 ? "technology sits" : "technologies sit"} on a prerequisite cycle, so that part of the tree is drawn out of order.`,
    );
  }

  // The second reading of depth, now that every depth is settled: what the column would be if each optional group were satisfied by its shallowest member.
  for (const node of workingNodes) {
    let deepest = -1;
    let shallowestOptional = Number.MAX_SAFE_INTEGER;
    let hasOptional = false;

    for (const prerequisiteId of node.prerequisiteIds) {
      const prerequisiteDepth = workingById.get(prerequisiteId)?.depth ?? 0;
      const isOptional = seenEdgeKeys.get(`${prerequisiteId}->${node.id}`)?.kind === "Optional";

      if (isOptional) {
        hasOptional = true;
        shallowestOptional = Math.min(shallowestOptional, prerequisiteDepth);
      } else {
        deepest = Math.max(deepest, prerequisiteDepth);
      }
    }

    if (hasOptional) {
      deepest = Math.max(deepest, shallowestOptional);
    }

    node.earliestDepth = deepest + 1;
  }

  // ── Which nodes are unconnected ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
  //
  // A technology with no edges in either direction tells the layered graph nothing, and a crowd of them inflates the first column with boxes that carry no
  // information. They go in a band of their own beneath the graph. An authored node is never treated this way: somebody placed it deliberately.

  for (const node of workingNodes) {
    node.isOrphan = !node.isAuthored && node.prerequisiteIds.length === 0 && node.dependentIds.length === 0;
  }

  const layeredNodes = workingNodes.filter((node) => !node.isOrphan);
  const orphanNodes = workingNodes.filter((node) => node.isOrphan);

  // ── Columns ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
  //
  // An authored column overrides the computed depth, so an authored node joins the column its author put it in. Both coordinates have to be set for that to
  // count: half a coordinate is an authoring mistake, not a position.

  for (const node of workingNodes) {
    if (!node.isAuthored) {
      if (node.technology.treeColumn !== null || node.technology.treeRow !== null) {
        warnings.push(`${node.technology.code} has only one of its tree column and tree row set, so both were ignored and it was laid out from the graph.`);
      }

      node.column = node.depth;
      continue;
    }

    const authoredColumn = node.technology.treeColumn ?? 0;

    if (authoredColumn < 0) {
      warnings.push(`${node.technology.code} has a negative tree column, which was clamped to 0.`);
    }

    node.column = Math.max(0, authoredColumn);
    node.placement = "authored";
  }

  const layerCount = layeredNodes.length === 0 ? 0 : Math.max(...layeredNodes.map((node) => node.column)) + 1;

  // ── Rows within a column ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
  //
  // Authored nodes are pinned: they hold the row they were given, are left out of the reordering, but still appear in the position map so the nodes that do
  // move compute their medians against them. That is the cheap rule that makes a partially authored catalogue behave the way an author expects — what they
  // placed stays put, and everything else flows around it.

  const pinnedRowsByColumn: Map<number, Map<number, number>> = new Map();
  const freeNodesByColumn: Map<number, number[]> = new Map();

  for (let column = 0; column < layerCount; column += 1) {
    pinnedRowsByColumn.set(column, new Map());
    freeNodesByColumn.set(column, []);
  }

  for (const node of layeredNodes) {
    if (!node.isAuthored) {
      freeNodesByColumn.get(node.column)?.push(node.id);
      continue;
    }

    const pinnedRows = pinnedRowsByColumn.get(node.column);

    if (pinnedRows === undefined) {
      continue;
    }

    const authoredRow = node.technology.treeRow ?? 0;

    if (authoredRow < 0) {
      warnings.push(`${node.technology.code} has a negative tree row, which was clamped to 0.`);
    }

    // Walk to the next free row if the cell is already taken. Because layeredNodes is in sort-order then id order, the first claimant wins and the loser
    // moves — the viewer never draws two boxes on top of each other, whatever the catalogue says.
    let row = Math.max(0, authoredRow);

    while (pinnedRows.has(row)) {
      const incumbentId = pinnedRows.get(row);
      const incumbent = incumbentId === undefined ? undefined : workingById.get(incumbentId);

      warnings.push(
        `${node.technology.code} and ${incumbent?.technology.code ?? "another technology"} are both authored at column ${node.column}, row ${row}. ${node.technology.code} was moved down.`,
      );
      row += 1;
    }

    pinnedRows.set(row, node.id);
    node.row = row;
  }

  /** Gives every unpinned node in a column the next row no pinned node has reserved, in the order the column's free list currently holds them. */
  function assignRows(column: number): void {
    const pinnedRows = pinnedRowsByColumn.get(column);
    const freeNodeIds = freeNodesByColumn.get(column);

    if (pinnedRows === undefined || freeNodeIds === undefined) {
      return;
    }

    let row = 0;

    for (const nodeId of freeNodeIds) {
      while (pinnedRows.has(row)) {
        row += 1;
      }

      const node = workingById.get(nodeId);

      if (node !== undefined) {
        node.row = row;
      }

      row += 1;
    }
  }

  for (let column = 0; column < layerCount; column += 1) {
    assignRows(column);
  }

  // ── Crossing reduction ──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
  //
  // Alternating median sweeps. Even sweeps run left to right and key each node on the median row of its prerequisites; odd sweeps run right to left and key
  // on its dependents. Every sweep is scored and the best arrangement is kept, not the last one: forward sweeps score far better than backward ones on this
  // graph, so keeping the winner rather than the final state is worth about a fifth of the total on its own.

  const positionsFor = (): PositionedNode[] => layeredNodes.map((node) => ({ id: node.id, column: node.column, row: node.row }));
  const directedEdges: DirectedEdge[] = resolvedEdges.map((edge) => ({ fromId: edge.fromId, toId: edge.toId }));

  let crossingCount = countCrossings(positionsFor(), directedEdges);

  if (crossingSweeps > 0 && layerCount > 1) {
    /** A copy of every column's free-list order, so the best arrangement can be restored after a worse sweep has overwritten it. */
    const snapshot = (): Map<number, number[]> => {
      const copy = new Map<number, number[]>();

      for (const [column, nodeIds] of freeNodesByColumn) {
        copy.set(column, [...nodeIds]);
      }

      return copy;
    };

    const restore = (saved: Map<number, number[]>): void => {
      for (const [column, nodeIds] of saved) {
        freeNodesByColumn.set(column, [...nodeIds]);
        assignRows(column);
      }
    };

    let bestOrder = snapshot();
    let bestCrossings = crossingCount;

    for (let sweep = 0; sweep < crossingSweeps; sweep += 1) {
      const forwards = sweep % 2 === 0;

      for (let step = 0; step < layerCount; step += 1) {
        const column = forwards ? step : layerCount - 1 - step;
        const freeNodeIds = freeNodesByColumn.get(column);

        if (freeNodeIds === undefined || freeNodeIds.length < 2) {
          continue;
        }

        // The key each node will be sorted on, and the row it currently holds, which breaks ties so the sort is stable and the sweep deterministic.
        const keyByNodeId = new Map<number, number>();
        const currentRowByNodeId = new Map<number, number>();

        for (const nodeId of freeNodeIds) {
          const node = workingById.get(nodeId);

          if (node === undefined) {
            continue;
          }

          currentRowByNodeId.set(nodeId, node.row);

          const neighbourIds = forwards ? node.prerequisiteIds : node.dependentIds;
          const neighbourRows: number[] = [];

          for (const neighbourId of neighbourIds) {
            const neighbour = workingById.get(neighbourId);

            // Only neighbours in the direction being swept carry information about where this node wants to be.
            if (neighbour === undefined || neighbour.isOrphan) {
              continue;
            }

            if (forwards ? neighbour.column < column : neighbour.column > column) {
              neighbourRows.push(neighbour.row);
            }
          }

          // A node with no neighbours in the sweep direction keys on the row it already has, not on zero — otherwise every edgeless node in the column
          // collapses to the top and shoves everything that does have neighbours out of the way.
          keyByNodeId.set(nodeId, neighbourRows.length === 0 ? node.row : medianOf(neighbourRows));
        }

        freeNodeIds.sort((firstId, secondId) => {
          const keyDelta = (keyByNodeId.get(firstId) ?? 0) - (keyByNodeId.get(secondId) ?? 0);

          if (keyDelta !== 0) {
            return keyDelta;
          }

          return (currentRowByNodeId.get(firstId) ?? 0) - (currentRowByNodeId.get(secondId) ?? 0);
        });

        assignRows(column);
      }

      const sweptCrossings = countCrossings(positionsFor(), directedEdges);

      // Strictly better only, so the earliest sweep to reach a score keeps it and the result does not depend on how many sweeps were asked for.
      if (sweptCrossings < bestCrossings) {
        bestCrossings = sweptCrossings;
        bestOrder = snapshot();
      }
    }

    restore(bestOrder);
    crossingCount = bestCrossings;
  }

  const layerSizes: number[] = [];

  for (let column = 0; column < layerCount; column += 1) {
    layerSizes.push(layeredNodes.filter((node) => node.column === column).length);
  }

  // ── The unconnected band ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
  //
  // Tiled across the same columns as the graph above it, in the same SVG and the same hit-test space. Entirely driven by the count: with nothing unconnected
  // there is no band, no divider, no label and no extra height, so the band disappears of its own accord once the catalogue's loose technologies are wired
  // into the tree.

  const layeredRowCount = layeredNodes.length === 0 ? 0 : Math.max(...layeredNodes.map((node) => node.row)) + 1;
  const layeredBottom = layeredRowCount === 0 ? padding : padding + layeredRowCount * rowPitch - rowGap;
  const orphanBandTop = orphanNodes.length === 0 ? null : layeredBottom + (layeredRowCount === 0 ? 0 : orphanBandGap);

  // With nothing but unconnected technologies there are no columns to tile across, so the band falls back to a single one.
  const bandColumns = Math.max(1, layerCount);
  let bandRowCount = 0;

  orphanNodes.forEach((node, index) => {
    node.column = index % bandColumns;
    node.row = Math.floor(index / bandColumns);
    node.placement = "orphanBand";
    bandRowCount = Math.max(bandRowCount, node.row + 1);
  });

  // ── Coordinates ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

  const nodes: LaidOutTechnologyNode[] = [];
  const nodesById = new Map<number, LaidOutTechnologyNode>();
  let maxColumnUsed = 0;

  for (const node of workingNodes) {
    const x = padding + node.column * columnPitch;
    const y = node.isOrphan ? (orphanBandTop ?? padding) + node.row * rowPitch : padding + node.row * rowPitch;

    const laidOut: LaidOutTechnologyNode = {
      id: node.id,
      code: node.technology.code,
      displayName: node.technology.displayName,
      label: truncateToWidth(node.technology.displayName, labelWidth),
      column: node.column,
      row: node.row,
      x,
      y,
      width: nodeWidth,
      height: nodeHeight,
      depth: node.depth,
      earliestDepth: node.earliestDepth,
      prerequisiteCount: node.prerequisiteIds.length,
      dependentCount: node.dependentIds.length,
      usesDirected: node.technology.directedRequired !== null,
      usesExperience: node.technology.experienceRequired !== null,
      usesDiffusion: node.technology.diffusionRequired !== null,
      isOrphan: node.isOrphan,
      isInCycle: node.isInCycle,
      placement: node.placement,
    };

    nodes.push(laidOut);
    nodesById.set(node.id, laidOut);
    maxColumnUsed = Math.max(maxColumnUsed, node.column);
  }

  const width = padding * 2 + (maxColumnUsed + 1) * columnPitch - layerGap;
  const height =
    orphanBandTop === null
      ? (layeredRowCount === 0 ? padding * 2 : padding * 2 + layeredRowCount * rowPitch - rowGap)
      : orphanBandTop + bandRowCount * rowPitch - rowGap + padding;

  // ── Edge geometry ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
  //
  // Cubic Béziers with horizontal tangents, leaving the source box's right edge and entering the target box's left edge. Leaving and entering horizontally is
  // what makes an edge's incidence readable *at the box*, which is the only question an edge exists to answer: with in-degrees up to six and out-degrees up
  // to eleven, straight lines arrive at a box edge at arbitrary angles and become impossible to tell apart.

  const edges: LaidOutTechnologyEdge[] = [];
  const incomingEdgeKeysById = new Map<number, string[]>();
  const outgoingEdgeKeysById = new Map<number, string[]>();

  for (const node of workingNodes) {
    incomingEdgeKeysById.set(node.id, []);
    outgoingEdgeKeysById.set(node.id, []);
  }

  for (const edge of resolvedEdges) {
    const from = nodesById.get(edge.fromId);
    const to = nodesById.get(edge.toId);

    if (from === undefined || to === undefined) {
      continue;
    }

    const startX = from.x + from.width;
    const startY = from.y + from.height / 2;
    const endX = to.x;
    const endY = to.y + to.height / 2;
    const deltaX = endX - startX;
    const span = to.column - from.column;

    let controlOffset: number;

    if (span <= 0) {
      // Only reachable through authored columns or the cycle fallback. Belly the curve well out and back so it reads as a detour rather than collapsing into
      // a straight line lying on top of the boxes. The dash pattern is not touched — dotted means optional, and nothing else.
      controlOffset = Math.max(columnPitch * 0.6, -deltaX * 0.5);
    } else if (span === 1) {
      controlOffset = Math.max(layerGap * 0.55, deltaX * 0.5);
    } else {
      // A longer edge bellies wider so it separates from the dense single-column traffic sharing the same gap. It may pass behind an intervening box, which
      // is fine: box fills are opaque and the edge layer draws first, so a box occludes an edge cleanly rather than slicing it.
      controlOffset = deltaX * 0.35 + columnPitch * 0.25;
    }

    const key = `${edge.fromId}->${edge.toId}`;

    edges.push({
      key,
      fromId: edge.fromId,
      toId: edge.toId,
      kind: edge.kind,
      span,
      path: `M${startX} ${startY} C${startX + controlOffset} ${startY} ${endX - controlOffset} ${endY} ${endX} ${endY}`,
    });

    outgoingEdgeKeysById.get(edge.fromId)?.push(key);
    incomingEdgeKeysById.get(edge.toId)?.push(key);
  }

  const prerequisiteIdsById = new Map<number, number[]>();
  const dependentIdsById = new Map<number, number[]>();

  for (const node of workingNodes) {
    prerequisiteIdsById.set(node.id, [...node.prerequisiteIds]);
    dependentIdsById.set(node.id, [...node.dependentIds]);
  }

  return {
    nodes,
    nodesById,
    edges,
    incomingEdgeKeysById,
    outgoingEdgeKeysById,
    prerequisiteIdsById,
    dependentIdsById,
    width,
    height,
    layerCount,
    layerSizes,
    orphanBandTop,
    orphanCount: orphanNodes.length,
    cycleNodeIds,
    droppedPrerequisites,
    warnings,
    crossingCount,
  };
}
