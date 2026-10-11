import { HiXMark } from "react-icons/hi2";
import { TbLayoutSidebarLeftCollapse, TbLayoutSidebarLeftExpand } from "react-icons/tb";
import { Button, SearchableComboBox } from "@/components/ui";
import type { CatalogueTechnology } from "../../api/technologyApi";

export interface TechnologyTreeHeaderProps {
  technologies: readonly CatalogueTechnology[];
  onPick: (technology: CatalogueTechnology) => void;
  nodeCount: number;
  edgeCount: number;
  optionalEdgeCount: number;
  zoomPercent: number;
  onFit: () => void;
  onReset: () => void;
  outlineOpen: boolean;
  onToggleOutline: () => void;
  onClose: () => void;
}

/**
 * The bar above the tree: what you are looking at, how to find one technology in it, and the zoom controls.
 *
 * The zoom is shown as a live percentage beside its two buttons, so the current state is legible rather than something you work out by how big the boxes
 * look. There is no era filter: every seeded technology is Stone-era, so the control would look broken, and era is documented as not being a UI dimension.
 */
export default function TechnologyTreeHeader({
  technologies,
  onPick,
  nodeCount,
  edgeCount,
  optionalEdgeCount,
  zoomPercent,
  onFit,
  onReset,
  outlineOpen,
  onToggleOutline,
  onClose,
}: TechnologyTreeHeaderProps) {
  return (
    <header className="flex shrink-0 items-center gap-3 border-b border-slate-200 bg-stone-100 px-3 py-2" data-testid="technology-tree-header">
      <button
        type="button"
        onClick={onToggleOutline}
        aria-label={outlineOpen ? "Hide the technology list" : "Show the technology list"}
        aria-pressed={outlineOpen}
        className="rounded p-1 text-slate-600 hover:bg-slate-200 focus:outline-none focus-visible:ring-2 focus-visible:ring-slate-400">
        {outlineOpen ? <TbLayoutSidebarLeftCollapse size={20} aria-hidden /> : <TbLayoutSidebarLeftExpand size={20} aria-hidden />}
      </button>

      <div>
        <h1 className="text-base leading-tight font-semibold text-slate-900">Technology Tree</h1>
        <p className="text-[11px] text-slate-500">
          {nodeCount} technologies · {edgeCount} prerequisites, {optionalEdgeCount} optional
        </p>
      </div>

      <div className="ml-auto w-72">
        <SearchableComboBox<CatalogueTechnology>
          items={[...technologies]}
          getKey={(technology) => technology.id}
          getPrimaryText={(technology) => technology.displayName}
          getSecondaryText={(technology) => technology.code}
          secondaryPlacement="right"
          getSearchText={(technology) => `${technology.displayName} ${technology.code}`}
          placeholder="Find a technology…"
          inputClassName="w-full rounded-md border border-slate-300 bg-white px-2 py-1.5 text-sm text-slate-900"
          onSelect={onPick}
        />
      </div>

      <div className="flex items-center gap-2">
        <span className="w-12 text-right font-mono text-xs text-slate-500" aria-label="Current zoom">
          {zoomPercent}%
        </span>
        <Button variant="outline-secondary" onClick={onFit}>
          Fit
        </Button>
        <Button variant="outline-secondary" onClick={onReset}>
          100%
        </Button>
      </div>

      <button
        type="button"
        onClick={onClose}
        aria-label="Close the technology tree"
        className="rounded p-1 text-slate-600 hover:bg-slate-200 focus:outline-none focus-visible:ring-2 focus-visible:ring-slate-400">
        <HiXMark size={20} aria-hidden />
      </button>
    </header>
  );
}
