import { useEffect, useMemo, useState } from "react";
import { FaChevronDown, FaChevronRight } from "react-icons/fa6";
import { Alert, Button, Modal, Spinner } from "@/components/ui";

import type { SeedTarget, SeedTargetGroup, SeedTargetInfo } from "../api/seedApi";
import { useSeedSelection } from "../hooks/useSeedSelection";

/** Headings for the groups the backend files targets under, in the order they are listed. */
const GROUP_LABELS: Record<SeedTargetGroup, string> = {
  Map: "Map",
  UnitDesign: "Unit Design",
};

export interface SeedSelectionDialogProps {
  open: boolean;
  /** The seed target catalogue, already ordered by group then sort order. */
  targets: SeedTargetInfo[];
  /** True while the catalogue is still loading, so the dialog can say so rather than look empty. */
  loading?: boolean;
  /** Populated when the catalogue could not be loaded; seeding is not offered in that case. */
  error?: string | null;
  onCancel: () => void;
  /** Called with the ticked targets plus every prerequisite they pulled in. */
  onCommit: (targets: SeedTarget[]) => void;
}

/**
 * Chooses which parts of the database the seed should write.
 *
 * Ticking a target ticks everything it depends on and locks those rows, so a selection can never be
 * submitted with a prerequisite missing. Unticking the parent releases them again. The locking is a
 * courtesy — the server expands the selection over its prerequisites regardless — but it means the user
 * can see what a tick really costs before committing to it.
 *
 * Selection resets each time the dialog opens, so a previous choice never leaks into the next seed.
 */
export default function SeedSelectionDialog({
  open,
  targets,
  loading = false,
  error,
  onCancel,
  onCommit,
}: SeedSelectionDialogProps) {
  const selection = useSeedSelection(targets);

  // Groups, in the order the catalogue lists them, with their targets kept in catalogue order.
  const groups = useMemo(() => {
    const byGroup = new Map<SeedTargetGroup, SeedTargetInfo[]>();

    for (const target of targets) {
      const existing = byGroup.get(target.group);

      if (existing) {
        existing.push(target);
      } else {
        byGroup.set(target.group, [target]);
      }
    }

    return [...byGroup.entries()];
  }, [targets]);

  // Which group headers are open. Every group starts expanded — there are only two, and a dialog that
  // opens with everything collapsed hides the very thing it exists to show.
  const [collapsedGroups, setCollapsedGroups] = useState<Set<SeedTargetGroup>>(new Set());

  // Reset both the ticks and the expand state on every open.
  const { clear } = selection;

  useEffect(() => {
    if (open) {
      clear();
      setCollapsedGroups(new Set());
    }
  }, [open, clear]);

  const toggleGroup = (group: SeedTargetGroup) => {
    setCollapsedGroups((previous) => {
      const next = new Set(previous);

      if (next.has(group)) {
        next.delete(group);
      } else {
        next.add(group);
      }

      return next;
    });
  };

  const selectedCount = selection.submitTargets.length;

  return (
    <Modal
      open={open}
      onClose={onCancel}
      title="Seed Database"
      size="lg"
      footer={
        <>
          <Button variant="secondary" onClick={onCancel}>
            Cancel
          </Button>
          <Button variant="danger" disabled={selectedCount === 0} onClick={() => onCommit(selection.submitTargets)}>
            {selectedCount === 0 ? "Seed" : `Seed ${selectedCount} Target${selectedCount === 1 ? "" : "s"}`}
          </Button>
        </>
      }>
      {error && (
        <Alert variant="danger" className="mb-3">
          {error}
        </Alert>
      )}

      <p className="mb-2 text-sm text-slate-600">
        Choose which parts of the database to seed. Existing rows for the selected areas are overwritten to
        match the seed values; anything you have added beyond them is left alone.
      </p>

      <p className="mb-3 text-xs text-slate-500">
        Seeding unit components also refreshes the compiled snapshot of every template built on them,
        including templates you authored by hand.
      </p>

      {loading && (
        <div className="flex items-center gap-2 py-6 text-sm text-slate-600">
          <Spinner /> Loading seed targets…
        </div>
      )}

      {loading === false && error == null && (
        <>
          <div className="mb-2 flex items-center gap-3 text-xs">
            <button type="button" className="text-slate-600 underline hover:text-slate-900" onClick={selection.selectAll}>
              Select all
            </button>
            <button type="button" className="text-slate-600 underline hover:text-slate-900" onClick={selection.clear}>
              Clear
            </button>
          </div>

          <div className="space-y-2">
            {groups.map(([group, groupTargets]) => {
              const expanded = collapsedGroups.has(group) === false;

              return (
                <div key={group} className="overflow-hidden rounded-md border border-slate-200 bg-white">
                  <button
                    type="button"
                    className="flex w-full items-center gap-3 bg-stone-50 px-3 py-2 text-left hover:bg-stone-100"
                    onClick={() => toggleGroup(group)}
                    aria-expanded={expanded}>
                    <span className="flex-1 font-medium text-slate-800">{GROUP_LABELS[group] ?? group}</span>
                    <span className="flex-none text-slate-600">{expanded ? <FaChevronDown /> : <FaChevronRight />}</span>
                  </button>

                  {expanded && (
                    <div className="space-y-1 px-2 py-2">
                      {groupTargets.map((target) => (
                        <SeedTargetRow key={target.target} target={target} selection={selection} />
                      ))}
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </>
      )}
    </Modal>
  );
}

interface SeedTargetRowProps {
  target: SeedTargetInfo;
  selection: ReturnType<typeof useSeedSelection>;
}

/**
 * One checkbox row: the target's label and description, its row count, and — when it is only ticked
 * because something else needs it — a note naming what pulled it in.
 */
function SeedTargetRow({ target, selection }: SeedTargetRowProps) {
  const isExplicit = selection.explicitTargets.has(target.target);
  const isImplied = selection.impliedTargets.has(target.target);

  // An implied row is ticked and locked: releasing it would leave the selection incoherent, and the only
  // sensible way to untick it is to untick whatever is asking for it.
  const requiredBy = isImplied ? selection.requiredByNames(target.target) : [];

  return (
    <label
      className={`flex items-start gap-2 rounded px-2 py-1.5 text-sm ${
        isImplied ? "bg-slate-50 text-slate-500" : "text-slate-700 hover:bg-slate-50"
      }`}>
      <input
        type="checkbox"
        className="mt-1"
        checked={isExplicit || isImplied}
        disabled={isImplied}
        onChange={() => selection.toggle(target.target)}
      />
      <span className="flex-1">
        <span className="font-medium">
          {target.displayName}
          <span className="ml-2 font-normal text-xs text-slate-400">
            {target.itemCount} row{target.itemCount === 1 ? "" : "s"}
          </span>
        </span>
        <span className="block text-xs text-slate-500">{target.description}</span>
        {requiredBy.length > 0 && (
          <span className="block text-xs italic text-slate-400">Required by {requiredBy.join(", ")}</span>
        )}
      </span>
    </label>
  );
}
