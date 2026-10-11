import { useCallback, useEffect, useMemo, useState } from "react";
import { Alert, Spinner } from "@/components/ui";
import { apiErrorMessages } from "@/util/apiErrors";
import type { CatalogueTechnology } from "../../api/technologyApi";
import { useSvgViewport } from "../../hooks/useSvgViewport";
import { useTechnologyCatalogue } from "../../hooks/useTechnologyCatalogue";
import { buildTechnologyTreeHighlight } from "../../tools/technologyTreeGraph";
import { layOutTechnologyTree } from "../../tools/technologyTreeLayout";
import TechnologyDetailsPanel, { type TechnologyDependentRef } from "./TechnologyDetailsPanel";
import TechnologyTreeCanvas from "./TechnologyTreeCanvas";
import TechnologyTreeHeader from "./TechnologyTreeHeader";
import TechnologyTreeLegend from "./TechnologyTreeLegend";
import TechnologyTreeOutline from "./TechnologyTreeOutline";

/** The catalogue stood in for while one is still loading. A module constant, so the empty case keeps a stable reference and the layout memo holds. */
const NO_TECHNOLOGIES: CatalogueTechnology[] = [];

export interface TechnologyTreeViewerProps {
  onClose: () => void;
  /**
   * A catalogue to display instead of fetching one.
   *
   * The app leaves it out and the viewer self-loads. Tests pass it, because there is no network mocking in this project — an injected catalogue is how a
   * component here gets exercised at all, the same seam UnitDesigner uses.
   */
  technologies?: CatalogueTechnology[];
  /** Only meaningful alongside an injected catalogue; the self-loading path takes these from the query. */
  loading?: boolean;
  errorMessages?: string[];
}

/**
 * The technology tree, read from the catalogue rather than from any game.
 *
 * The only component here holding state. Everything below it takes props and reports back, so the pieces that matter — the layout and the traversal — stay
 * pure and separately testable, and the pan path has somewhere to stop re-rendering.
 *
 * Deliberately has no hovered-node state: hover is handled entirely in CSS on the node itself, because a hovered id up here would re-render a couple of
 * hundred boxes on every pointer move across the canvas.
 */
export default function TechnologyTreeViewer({ onClose, technologies, loading, errorMessages }: TechnologyTreeViewerProps) {
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [showFullAncestry, setShowFullAncestry] = useState(false);
  const [outlineOpen, setOutlineOpen] = useState(true);
  const [filterText, setFilterText] = useState("");

  // Called unconditionally, and switched off rather than skipped, because a hook behind a condition is a hook that breaks on the render where the condition
  // changes.
  const catalogueQuery = useTechnologyCatalogue({ enabled: technologies === undefined });

  const catalogue = technologies ?? catalogueQuery.data ?? NO_TECHNOLOGIES;
  const isLoading = loading ?? (technologies === undefined && catalogueQuery.isLoading);
  const errors = errorMessages ?? (catalogueQuery.error === null ? [] : apiErrorMessages(catalogueQuery.error));

  // React-query hands back the same array reference while the data is cached, so the layout runs once per fetch rather than once per render.
  const layout = useMemo(() => layOutTechnologyTree(catalogue), [catalogue]);

  const viewport = useSvgViewport({ width: layout.width, height: layout.height });

  const highlight = useMemo(() => buildTechnologyTreeHighlight(layout, selectedId, showFullAncestry), [layout, selectedId, showFullAncestry]);

  const technologyById = useMemo(() => new Map(catalogue.map((technology) => [technology.id, technology])), [catalogue]);

  const selectedTechnology = selectedId === null ? null : technologyById.get(selectedId) ?? null;
  const selectedNode = selectedId === null ? null : layout.nodesById.get(selectedId) ?? null;

  /** The technologies that need the selected one, each labelled with how *it* lists the dependency. */
  const dependents = useMemo<TechnologyDependentRef[]>(() => {
    if (selectedId === null) {
      return [];
    }

    const dependentIds = layout.dependentIdsById.get(selectedId) ?? [];

    return dependentIds.flatMap((dependentId) => {
      const dependent = technologyById.get(dependentId);
      const edge = dependent?.prerequisites.find((prerequisite) => prerequisite.prerequisiteTechnologyId === selectedId);

      if (dependent === undefined || edge === undefined) {
        return [];
      }

      return [{ id: dependent.id, code: dependent.code, displayName: dependent.displayName, kind: edge.prerequisiteKind }];
    });
  }, [selectedId, layout, technologyById]);

  const droppedForSelection = useMemo(
    () => layout.droppedPrerequisites.filter((dropped) => dropped.technologyId === selectedId),
    [layout, selectedId],
  );

  /** Selects a technology and brings it into view, which is what both the header search and the outline rail want. */
  const selectAndReveal = useCallback(
    (id: number) => {
      setSelectedId(id);

      const node = layout.nodesById.get(id);

      if (node !== undefined) {
        viewport.centreOn({ x: node.x + node.width / 2, y: node.y + node.height / 2 });
      }
    },
    [layout, viewport],
  );

  // Escape clears a selection if there is one and closes the viewer otherwise, so it never shuts the whole overlay when the reader only meant to deselect.
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== "Escape") {
        return;
      }

      if (selectedId !== null) {
        setSelectedId(null);
        return;
      }

      onClose();
    };

    window.addEventListener("keydown", onKeyDown);

    return () => window.removeEventListener("keydown", onKeyDown);
  }, [selectedId, onClose]);

  const optionalEdgeCount = useMemo(() => layout.edges.filter((edge) => edge.kind === "Optional").length, [layout]);

  return (
    // The text colour is set here as well as the background: the overlay mounts inside the Entity Setup Tool's pale-on-dark root, so without it every
    // element that does not name its own colour inherits one meant for a dark surface.
    <div className="flex h-full min-h-0 flex-col bg-white text-slate-900" data-testid="technology-tree-viewer">
      <TechnologyTreeHeader
        technologies={catalogue}
        onPick={(technology) => selectAndReveal(technology.id)}
        nodeCount={layout.nodes.length}
        edgeCount={layout.edges.length}
        optionalEdgeCount={optionalEdgeCount}
        zoomPercent={Math.round(viewport.scale * 100)}
        onFit={viewport.fitToContent}
        onReset={viewport.resetZoom}
        outlineOpen={outlineOpen}
        onToggleOutline={() => setOutlineOpen((open) => !open)}
        onClose={onClose}
      />

      {isLoading && (
        <div className="flex flex-1 items-center justify-center gap-2 text-sm text-slate-500">
          <Spinner />
          Loading the technology catalogue…
        </div>
      )}

      {!isLoading && errors.length > 0 && (
        <div className="p-4">
          <Alert variant="danger">
            <ul className="list-inside list-disc">
              {errors.map((message) => (
                <li key={message}>{message}</li>
              ))}
            </ul>
          </Alert>
        </div>
      )}

      {!isLoading && errors.length === 0 && (
        <div className="flex min-h-0 flex-1">
          {outlineOpen && (
            <TechnologyTreeOutline
              layout={layout}
              selectedId={selectedId}
              onSelect={selectAndReveal}
              filterText={filterText}
              onFilterTextChange={setFilterText}
            />
          )}

          <div className="relative min-w-0 flex-1">
            <TechnologyTreeCanvas
              layout={layout}
              nodeStates={highlight.nodeStates}
              emphasisedEdgeKeys={highlight.emphasisedEdgeKeys}
              dimUnrelated={highlight.dimUnrelated}
              onSelect={setSelectedId}
              viewport={viewport}
            />

            <TechnologyTreeLegend
              optionalEdgeCount={optionalEdgeCount}
              orphanCount={layout.orphanCount}
              cycleNodeCount={layout.cycleNodeIds.length}
              droppedPrerequisiteCount={layout.droppedPrerequisites.length}
              warnings={layout.warnings}
            />
          </div>

          <div className="w-96 shrink-0 border-l border-slate-200">
            <TechnologyDetailsPanel
              technology={selectedTechnology}
              node={selectedNode}
              dependents={dependents}
              droppedPrerequisites={droppedForSelection}
              showFullAncestry={showFullAncestry}
              onToggleFullAncestry={setShowFullAncestry}
              onNavigate={selectAndReveal}
            />
          </div>
        </div>
      )}
    </div>
  );
}
