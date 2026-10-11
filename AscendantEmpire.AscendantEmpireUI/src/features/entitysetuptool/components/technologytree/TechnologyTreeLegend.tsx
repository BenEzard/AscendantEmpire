import { TechnologyEdgeMarkers } from "./TechnologyEdgeLayer";

export interface TechnologyTreeLegendProps {
  optionalEdgeCount: number;
  orphanCount: number;
  cycleNodeCount: number;
  droppedPrerequisiteCount: number;
  warnings: readonly string[];
}

/**
 * What the two line styles mean, and anything wrong with the catalogue.
 *
 * Both styles are described **in words, with the rule attached** — "every one" against "any one" — rather than just named. The line style and the arrowhead
 * shape are the quick signal; this is the one that does not depend on being able to see the difference.
 *
 * Sits outside the SVG, pinned to the corner of the canvas pane, so it stays put while the tree pans underneath it. Each warning row appears only when its
 * count is non-zero, so a clean catalogue shows nothing but the two line styles.
 */
export default function TechnologyTreeLegend({
  optionalEdgeCount,
  orphanCount,
  cycleNodeCount,
  droppedPrerequisiteCount,
  warnings,
}: TechnologyTreeLegendProps) {
  const problems: string[] = [];

  if (orphanCount > 0) {
    problems.push(`${orphanCount} ${orphanCount === 1 ? "technology has" : "technologies have"} no prerequisites and no dependents, shown in the band below.`);
  }

  if (cycleNodeCount > 0) {
    problems.push(`${cycleNodeCount} ${cycleNodeCount === 1 ? "technology sits" : "technologies sit"} on a prerequisite cycle.`);
  }

  if (droppedPrerequisiteCount > 0) {
    problems.push(`${droppedPrerequisiteCount} ${droppedPrerequisiteCount === 1 ? "prerequisite" : "prerequisites"} could not be resolved and are not drawn.`);
  }

  return (
    <div
      className="pointer-events-none absolute bottom-3 left-3 max-w-xs rounded-md border border-slate-200 bg-white/95 p-3 shadow-sm"
      data-testid="technology-tree-legend">
      <ul className="flex flex-col gap-2">
        <li className="flex items-start gap-2">
          {/* The same markers the canvas uses, so the sample cannot drift from the real thing. */}
          <svg width={44} height={14} aria-hidden="true" className="mt-0.5 shrink-0">
            <defs>
              <TechnologyEdgeMarkers />
            </defs>
            <line x1={2} y1={7} x2={34} y2={7} className="stroke-slate-400" strokeWidth={1.5} markerEnd="url(#technology-arrow-mandatory)" />
          </svg>
          <span className="text-xs text-slate-700">
            <span className="font-semibold">Solid</span> — Mandatory: every one must be discovered
          </span>
        </li>

        <li className="flex items-start gap-2">
          <svg width={44} height={14} aria-hidden="true" className="mt-0.5 shrink-0">
            <line
              x1={2}
              y1={7}
              x2={34}
              y2={7}
              className="stroke-slate-400"
              strokeWidth={1.5}
              strokeDasharray="5 4"
              markerEnd="url(#technology-arrow-optional)"
            />
          </svg>
          <span className="text-xs text-slate-700">
            <span className="font-semibold">Dotted</span> — Optional: any one of them is enough
            {optionalEdgeCount > 0 && <span className="text-slate-400"> ({optionalEdgeCount})</span>}
          </span>
        </li>
      </ul>

      {problems.length > 0 && (
        <ul className="mt-3 flex flex-col gap-1 border-t border-slate-200 pt-2">
          {problems.map((problem) => (
            <li key={problem} className="text-[11px] text-[var(--bs-danger)]">
              {problem}
            </li>
          ))}
        </ul>
      )}

      {warnings.length > 0 && (
        <ul className="mt-2 flex flex-col gap-1">
          {warnings.slice(0, 3).map((warning) => (
            <li key={warning} className="text-[11px] text-slate-500">
              {warning}
            </li>
          ))}
          {warnings.length > 3 && <li className="text-[11px] text-slate-400">and {warnings.length - 3} more.</li>}
        </ul>
      )}
    </div>
  );
}
