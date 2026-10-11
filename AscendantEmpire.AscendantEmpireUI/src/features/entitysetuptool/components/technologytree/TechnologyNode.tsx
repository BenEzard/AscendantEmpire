import React from "react";
import type { TechnologyNodeState } from "../../tools/technologyTreeGraph";
import type { LaidOutTechnologyNode } from "../../tools/technologyTreeLayout";

/** How each state paints the box. Every related state also differs in stroke weight, so the three are never told apart by colour alone. */
const STATE_CLASSES: Record<TechnologyNodeState, { rect: string; strokeWidth: number; groupOpacity: number }> = {
  base: { rect: "fill-white stroke-slate-300", strokeWidth: 1, groupOpacity: 1 },
  selected: { rect: "fill-selection-wash stroke-selection", strokeWidth: 2.5, groupOpacity: 1 },
  prerequisite: { rect: "fill-sky-50 stroke-sky-500", strokeWidth: 2, groupOpacity: 1 },
  dependent: { rect: "fill-emerald-50 stroke-emerald-500", strokeWidth: 2, groupOpacity: 1 },
  // Faded rather than recoloured: "not related to what you picked" is a matter of prominence, and fading says it without needing a hue of its own.
  dimmed: { rect: "fill-white stroke-slate-200", strokeWidth: 1, groupOpacity: 0.3 },
};

/** Where the bucket dots sit, measured in from the box's right edge. */
const BUCKET_DOT_RADIUS = 2.5;
const BUCKET_DOT_SPACING = 9;
const BUCKET_DOT_INSET = 11;

export interface TechnologyNodeProps {
  node: LaidOutTechnologyNode;
  state: TechnologyNodeState;
  onSelect: (id: number) => void;
}

/**
 * One technology, as a box on the canvas.
 *
 * Memoised and given only primitives and a stable callback, because there are a couple of hundred of these and a pan must not re-render any of them.
 *
 * Hover is left entirely to CSS — a hovered id in React state would re-render the whole node layer on every pointer move across the canvas, and nothing
 * outside the box needs to know what the pointer is over. The tooltip is a native SVG title for the same reason.
 */
const TechnologyNode = React.memo(function TechnologyNode({ node, state, onSelect }: TechnologyNodeProps) {
  const styling = STATE_CLASSES[state];

  const buckets: string[] = [];

  if (node.usesDirected) {
    buckets.push("directed");
  }

  if (node.usesExperience) {
    buckets.push("experience");
  }

  if (node.usesDiffusion) {
    buckets.push("diffusion");
  }

  const prerequisiteSummary = `${node.prerequisiteCount} ${node.prerequisiteCount === 1 ? "prerequisite" : "prerequisites"}`;
  const dependentSummary = `${node.dependentCount} ${node.dependentCount === 1 ? "dependent" : "dependents"}`;
  const tooltip = `${node.displayName} — ${prerequisiteSummary}, ${dependentSummary}`;

  return (
    <g
      className="group cursor-pointer"
      transform={`translate(${node.x} ${node.y})`}
      opacity={styling.groupOpacity}
      data-state={state}
      data-testid={`technology-node-${node.id}`}
      onClick={() => onSelect(node.id)}>
      <title>{tooltip}</title>

      <rect
        width={node.width}
        height={node.height}
        rx={6}
        strokeWidth={styling.strokeWidth}
        className={`${styling.rect} group-hover:stroke-slate-600`}
      />

      {/* A notch across the top-right corner of the selected box, so the selection reads without relying on the selection colour. */}
      {state === "selected" && (
        <path d={`M${node.width - 14} 0 L${node.width} 0 L${node.width} 14 Z`} className="fill-selection" data-testid={`technology-node-notch-${node.id}`} />
      )}

      <text x={8} y={20} className="pointer-events-none fill-slate-900 text-[13px] font-medium">
        {node.label}
      </text>
      <text x={8} y={36} className="pointer-events-none fill-slate-500 text-[10px]">
        {node.code}
      </text>

      {/* One dot per research bucket the technology actually uses. A bucket it does not use has no dot at all, matching the data model, where an unused
          bucket has no requirement rather than a requirement of zero. */}
      {buckets.map((bucket, index) => (
        <circle
          key={bucket}
          cx={node.width - BUCKET_DOT_INSET - index * BUCKET_DOT_SPACING}
          cy={node.height - 11}
          r={BUCKET_DOT_RADIUS}
          className="pointer-events-none fill-slate-400"
          data-bucket={bucket}
        />
      ))}

      {/* Only reachable from a catalogue the upsert would have rejected, but the whole point of showing it is that somebody can see it went wrong. */}
      {node.isInCycle && (
        <text x={node.width - 8} y={20} textAnchor="end" className="pointer-events-none fill-[var(--bs-danger)] text-[12px] font-bold">
          ⟲
        </text>
      )}
    </g>
  );
});

export default TechnologyNode;
