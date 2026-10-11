import type { UseSvgViewportResult } from "../../hooks/useSvgViewport";
import type { TechnologyNodeState } from "../../tools/technologyTreeGraph";
import type { TechnologyTreeLayout } from "../../tools/technologyTreeLayout";
import TechnologyEdgeLayer, { TechnologyEdgeMarkers } from "./TechnologyEdgeLayer";
import TechnologyNodeLayer from "./TechnologyNodeLayer";

export interface TechnologyTreeCanvasProps {
  layout: TechnologyTreeLayout;
  nodeStates: ReadonlyMap<number, TechnologyNodeState>;
  emphasisedEdgeKeys: ReadonlySet<string>;
  dimUnrelated: boolean;
  onSelect: (id: number | null) => void;
  viewport: UseSvgViewportResult;
}

/**
 * The tree itself: one SVG, panned and zoomed by its viewBox.
 *
 * Edges are drawn before nodes, which is what lets a long edge pass behind an intervening box: the box fills are opaque, so a box occludes the edge cleanly
 * rather than the edge slicing through it. That is also why the boxes carry no drop shadow — an SVG filter on a couple of hundred nodes is the classic
 * performance cliff, and it would soften exactly the occlusion this relies on.
 *
 * Exposed to assistive technology as a single image with a summary, not as a couple of hundred focusable shapes. The outline rail beside it is the real
 * non-visual representation of the graph, and the keyboard route into it.
 */
export default function TechnologyTreeCanvas({
  layout,
  nodeStates,
  emphasisedEdgeKeys,
  dimUnrelated,
  onSelect,
  viewport,
}: TechnologyTreeCanvasProps) {
  const columnSummary = layout.layerSizes.map((size, column) => `column ${column}: ${size}`).join(", ");

  // Clicking the background clears the selection — but not when the pointer was dragging the canvas, or every pan would end by deselecting.
  const handleBackgroundClick = () => {
    if (viewport.didPan()) {
      return;
    }

    onSelect(null);
  };

  return (
    <div
      ref={viewport.containerRef}
      tabIndex={0}
      onKeyDown={viewport.handleKeyDown}
      className="h-full w-full overflow-hidden bg-slate-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-slate-400"
      data-testid="technology-tree-canvas">
      <svg
        ref={viewport.svgRef}
        width="100%"
        height="100%"
        viewBox={viewport.viewBox}
        role="img"
        aria-label={`Technology tree: ${layout.nodes.length} technologies, ${layout.edges.length} prerequisite links across ${layout.layerCount} columns.`}
        className={viewport.isPanning ? "cursor-grabbing" : "cursor-grab"}
        {...viewport.pointerHandlers}>
        <desc>{`Technologies per column — ${columnSummary}.`}</desc>

        <defs>
          <TechnologyEdgeMarkers />
        </defs>

        {/* The click target for clearing a selection. Sized to the whole canvas rather than the viewport, so it is behind every box wherever the view is. */}
        <rect x={0} y={0} width={layout.width} height={layout.height} fill="transparent" onClick={handleBackgroundClick} data-testid="technology-tree-background" />

        {/* The unconnected band's divider and label. Driven entirely by the count, so with nothing unconnected there is no line, no caption and no gap —
            which is how the band disappears of its own accord once every technology is wired into the tree. */}
        {layout.orphanBandTop !== null && (
          <g data-testid="technology-tree-orphan-band">
            <line
              x1={0}
              y1={layout.orphanBandTop - 28}
              x2={layout.width}
              y2={layout.orphanBandTop - 28}
              className="stroke-slate-300"
              strokeWidth={1}
              strokeDasharray="2 6"
            />
            <text x={32} y={layout.orphanBandTop - 10} className="fill-slate-500 text-[13px] font-medium">
              {`Unconnected — ${layout.orphanCount} ${layout.orphanCount === 1 ? "technology" : "technologies"} with no prerequisites and no dependents`}
            </text>
          </g>
        )}

        <TechnologyEdgeLayer edges={layout.edges} emphasisedKeys={emphasisedEdgeKeys} dimOthers={dimUnrelated} />
        <TechnologyNodeLayer nodes={layout.nodes} nodeStates={nodeStates} onSelect={onSelect} />
      </svg>
    </div>
  );
}
