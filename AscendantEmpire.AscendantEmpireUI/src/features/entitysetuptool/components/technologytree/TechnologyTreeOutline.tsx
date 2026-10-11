import { useMemo } from "react";
import { inputClass } from "@/components/ui";
import type { LaidOutTechnologyNode, TechnologyTreeLayout } from "../../tools/technologyTreeLayout";

/** What a row reads as: the technology's name, then its prerequisite and dependent counts. The key to the two numbers sits under the filter box. */
function rowLabel(node: LaidOutTechnologyNode): string {
  return `${node.displayName} (${node.prerequisiteCount} / ${node.dependentCount})`;
}

/** The same two numbers in words, for the accessible name. */
function edgeSummary(node: LaidOutTechnologyNode): string {
  const prerequisites = `${node.prerequisiteCount} ${node.prerequisiteCount === 1 ? "prerequisite" : "prerequisites"}`;
  const dependents = `${node.dependentCount} ${node.dependentCount === 1 ? "dependent" : "dependents"}`;

  return `${prerequisites}, ${dependents}`;
}

export interface TechnologyTreeOutlineProps {
  layout: TechnologyTreeLayout;
  selectedId: number | null;
  onSelect: (id: number) => void;
  filterText: string;
  onFilterTextChange: (filterText: string) => void;
}

/**
 * Every technology as a list, grouped by column.
 *
 * This is the graph's non-visual representation, and the only keyboard route into it: a couple of hundred focusable shapes inside an SVG would be far worse
 * for a keyboard or screen-reader user than one well-ordered list, so the node boxes are deliberately not focusable and this carries the burden instead.
 *
 * Visible rather than screen-reader-only on purpose. A hidden parallel UI rots without anyone noticing, and this one has a second job anyway — finding one
 * technology among a couple of hundred by reading rather than by hunting across the canvas.
 */
export default function TechnologyTreeOutline({ layout, selectedId, onSelect, filterText, onFilterTextChange }: TechnologyTreeOutlineProps) {
  const groups = useMemo(() => {
    const needle = filterText.trim().toLowerCase();
    const matches = layout.nodes.filter(
      (node) => needle === "" || node.displayName.toLowerCase().includes(needle) || node.code.toLowerCase().includes(needle),
    );

    // Unconnected technologies get a heading of their own, matching the band they are drawn in — and only when there are any.
    const layered = matches.filter((node) => !node.isOrphan);
    const orphans = matches.filter((node) => node.isOrphan);

    const byColumn = Array.from({ length: layout.layerCount }, (_unused, column) => ({
      key: `column-${column}`,
      heading: `Column ${column}`,
      nodes: layered.filter((node) => node.column === column).sort((first, second) => first.row - second.row),
    })).filter((group) => group.nodes.length > 0);

    if (orphans.length > 0) {
      byColumn.push({ key: "unconnected", heading: "Unconnected", nodes: orphans });
    }

    return byColumn;
  }, [layout, filterText]);

  const matchCount = groups.reduce((total, group) => total + group.nodes.length, 0);

  return (
    <div className="flex h-full w-72 shrink-0 flex-col border-r border-slate-200 bg-white" data-testid="technology-tree-outline">
      <div className="shrink-0 border-b border-slate-200 p-3">
        <input
          type="search"
          value={filterText}
          onChange={(event) => onFilterTextChange(event.target.value)}
          placeholder="Filter technologies…"
          aria-label="Filter technologies"
          // The shared inputClass sets no text colour, so without this the field inherits the Entity Setup Tool's pale-on-dark body colour and the typed text
          // comes out washed out against the white field.
          className={`${inputClass} text-black`}
        />
        <p className="mt-1 text-[11px] text-slate-400">
          {matchCount} of {layout.nodes.length} — name (prerequisites / dependents)
        </p>
      </div>

      <nav className="min-h-0 flex-1 overflow-y-auto p-2" aria-label="Technologies by column">
        {groups.length === 0 && <p className="p-2 text-sm text-slate-400">Nothing matches that.</p>}

        {groups.map((group) => (
          <section key={group.key} className="mb-3">
            <h3 className="px-2 pb-1 text-xs font-semibold tracking-wide text-slate-500 uppercase">
              {group.heading}
              {/* A literal space, not just the margin: the margin is visual, and without it the accessible name runs the two together. */}
              {" "}
              <span className="font-normal text-slate-400">
                — {group.nodes.length} {group.nodes.length === 1 ? "technology" : "technologies"}
              </span>
            </h3>

            <ul>
              {group.nodes.map((node) => (
                <li key={node.id}>
                  <button
                    type="button"
                    onClick={() => onSelect(node.id)}
                    aria-current={selectedId === node.id ? "true" : undefined}
                    // The visible text is compact, which is what makes a column of a couple of hundred rows scannable, but "(4 / 1)" read aloud is noise. The
                    // accessible name opens the two numbers out, and starts with the visible label so the two cannot be heard as different controls.
                    aria-label={`${rowLabel(node)} — ${edgeSummary(node)}`}
                    className={[
                      "w-full rounded px-2 py-1 text-left text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-slate-400",
                      selectedId === node.id ? "border border-selection/55 bg-selection-tint/18 font-semibold text-slate-900" : "text-slate-700 hover:bg-slate-100",
                    ].join(" ")}>
                    {rowLabel(node)}
                  </button>
                </li>
              ))}
            </ul>
          </section>
        ))}
      </nav>
    </div>
  );
}
