import React from "react";
import type { LaidOutTechnologyEdge } from "../../tools/technologyTreeLayout";

// Mandatory and optional edges differ in three ways at once — dash pattern, arrowhead shape, and the wording of the details panel's headings. The dash alone
// is not enough: a four-on-four-off pattern on a one-pixel stroke stops being distinguishable well before the zoom-out limit, and disappears entirely in
// greyscale. The arrowhead shape survives both.

/** The arrowhead marker ids, so the defs and the paths that reference them cannot drift apart. */
const MARKER_IDS = {
  mandatoryBase: "technology-arrow-mandatory",
  optionalBase: "technology-arrow-optional",
  mandatoryEmphasised: "technology-arrow-mandatory-emphasised",
  optionalEmphasised: "technology-arrow-optional-emphasised",
} as const;

/** The dash pattern an optional edge is drawn with. */
const OPTIONAL_DASH = "5 4";

const BASE_STROKE_WIDTH = 1.5;
const EMPHASISED_STROKE_WIDTH = 2.25;

/** How faded an unrelated edge goes once something is selected. Low enough that the emphasised edges stand out, high enough that the shape is still there. */
const DIMMED_OPACITY = 0.25;

/**
 * The arrowhead definitions the edge paths reference.
 *
 * Rendered by the canvas inside its defs, but declared here so the shapes and the ids that point at them stay in one file. Sized in user space rather than in
 * stroke widths, so an emphasised edge gets a thicker line without also getting a bigger arrowhead.
 */
export function TechnologyEdgeMarkers() {
  const markers: { id: string; filled: boolean; className: string }[] = [
    { id: MARKER_IDS.mandatoryBase, filled: true, className: "fill-slate-400" },
    { id: MARKER_IDS.optionalBase, filled: false, className: "stroke-slate-400" },
    { id: MARKER_IDS.mandatoryEmphasised, filled: true, className: "fill-slate-700" },
    { id: MARKER_IDS.optionalEmphasised, filled: false, className: "stroke-slate-700" },
  ];

  return (
    <>
      {markers.map((marker) => (
        <marker
          key={marker.id}
          id={marker.id}
          viewBox="0 0 10 10"
          refX={9.5}
          refY={5}
          markerWidth={9}
          markerHeight={9}
          markerUnits="userSpaceOnUse"
          orient="auto-start-reverse">
          {marker.filled ? (
            // A solid triangle for mandatory: the heavier of the two shapes, for the stricter of the two requirements.
            <path d="M0 0 L10 5 L0 10 Z" className={marker.className} />
          ) : (
            // An open chevron for optional, which reads as lighter at every zoom without depending on the dash pattern being visible.
            <path d="M0.5 0.5 L9.5 5 L0.5 9.5" fill="none" strokeWidth={1.75} strokeLinecap="round" strokeLinejoin="round" className={marker.className} />
          )}
        </marker>
      ))}
    </>
  );
}

export interface TechnologyEdgeLayerProps {
  edges: readonly LaidOutTechnologyEdge[];
  /** The edges belonging to the current selection, drawn last and heavier. */
  emphasisedKeys: ReadonlySet<string>;
  /** Whether to fade everything that is not emphasised, which is true exactly when something is selected. */
  dimOthers: boolean;
}

/**
 * Every prerequisite edge, in four groups.
 *
 * Grouped by shared stroke attributes, as RiverStrokeLayer is, so the dash pattern and colour are set once per group rather than on every path. The two
 * emphasised groups come last so a highlighted edge sits above the rest without the array ever having to be reordered.
 *
 * Memoised, and for the same reason as the node layer: the viewBox changes on the canvas above it, and a couple of hundred paths must not re-render per pan
 * frame. Marked aria-hidden because an edge conveys nothing to a screen reader that the details panel does not say in words.
 */
const TechnologyEdgeLayer = React.memo(function TechnologyEdgeLayer({ edges, emphasisedKeys, dimOthers }: TechnologyEdgeLayerProps) {
  const groups = [
    {
      testId: "technology-edges-mandatory",
      className: "stroke-slate-400",
      dash: undefined,
      marker: MARKER_IDS.mandatoryBase,
      strokeWidth: BASE_STROKE_WIDTH,
      opacity: dimOthers ? DIMMED_OPACITY : 1,
      edges: edges.filter((edge) => edge.kind === "Mandatory" && !emphasisedKeys.has(edge.key)),
    },
    {
      testId: "technology-edges-optional",
      className: "stroke-slate-400",
      dash: OPTIONAL_DASH,
      marker: MARKER_IDS.optionalBase,
      strokeWidth: BASE_STROKE_WIDTH,
      opacity: dimOthers ? DIMMED_OPACITY : 1,
      edges: edges.filter((edge) => edge.kind === "Optional" && !emphasisedKeys.has(edge.key)),
    },
    {
      testId: "technology-edges-mandatory-emphasised",
      className: "stroke-slate-700",
      dash: undefined,
      marker: MARKER_IDS.mandatoryEmphasised,
      strokeWidth: EMPHASISED_STROKE_WIDTH,
      opacity: 1,
      edges: edges.filter((edge) => edge.kind === "Mandatory" && emphasisedKeys.has(edge.key)),
    },
    {
      testId: "technology-edges-optional-emphasised",
      className: "stroke-slate-700",
      dash: OPTIONAL_DASH,
      marker: MARKER_IDS.optionalEmphasised,
      strokeWidth: EMPHASISED_STROKE_WIDTH,
      opacity: 1,
      edges: edges.filter((edge) => edge.kind === "Optional" && emphasisedKeys.has(edge.key)),
    },
  ];

  return (
    <g aria-hidden="true" data-testid="technology-edge-layer">
      {groups.map((group) => (
        <g
          key={group.testId}
          data-testid={group.testId}
          className={group.className}
          fill="none"
          strokeWidth={group.strokeWidth}
          strokeDasharray={group.dash}
          opacity={group.opacity}>
          {group.edges.map((edge) => (
            <path key={edge.key} d={edge.path} markerEnd={`url(#${group.marker})`} data-edge-kind={edge.kind} data-edge-span={edge.span} />
          ))}
        </g>
      ))}
    </g>
  );
});

export default TechnologyEdgeLayer;
