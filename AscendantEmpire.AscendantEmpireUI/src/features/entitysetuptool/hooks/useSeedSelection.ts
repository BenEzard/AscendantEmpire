import { useCallback, useMemo, useState } from "react";
import type { SeedTarget, SeedTargetInfo } from "../api/seedApi";

export interface UseSeedSelectionResult {
  /** The targets the user ticked themselves. */
  explicitTargets: Set<SeedTarget>;
  /**
   * Targets pulled in because something explicitly ticked depends on them. Disjoint from
   * `explicitTargets` — a target the user ticked is explicit even when something else also needs it.
   */
  impliedTargets: Set<SeedTarget>;
  /** Everything that will be sent: the explicit ticks closed over their prerequisites. */
  submitTargets: SeedTarget[];
  /** For an implied target, the display names of the ticked targets that pulled it in. */
  requiredByNames: (target: SeedTarget) => string[];
  toggle: (target: SeedTarget) => void;
  selectAll: () => void;
  clear: () => void;
}

/**
 * Holds which seed targets are ticked, and derives the prerequisites those ticks drag in with them.
 *
 * Only the explicit ticks are stored. The implied set is recomputed from them on every render, which is
 * what makes unticking a parent release its prerequisites for free — there is no stored state to undo, so
 * the two can never disagree about why a row is ticked.
 *
 * The closure is a convenience, not the rule: the server expands the selection again before it runs, so a
 * disagreement between the two would cost an over-tick in the dialog, never an under-seeded database.
 */
export function useSeedSelection(targets: SeedTargetInfo[]): UseSeedSelectionResult {
  const [explicitTargets, setExplicitTargets] = useState<Set<SeedTarget>>(new Set());

  // Index by key so the dependency walk is a lookup rather than a scan per step.
  const targetsByKey = useMemo(() => {
    return new Map(targets.map((target) => [target.target, target]));
  }, [targets]);

  // Every target reachable from an explicit tick by following dependsOn, minus the explicit ones
  // themselves. Depth-first with a visited set, so a diamond in the graph is walked once and a cycle
  // (which the backend tests forbid, but which must not hang the browser either) terminates.
  const impliedTargets = useMemo(() => {
    const implied = new Set<SeedTarget>();
    const pending: SeedTarget[] = [];

    for (const target of explicitTargets) {
      pending.push(...(targetsByKey.get(target)?.dependsOn ?? []));
    }

    while (pending.length > 0) {
      const target = pending.pop()!;

      if (implied.has(target)) {
        continue;
      }

      implied.add(target);
      pending.push(...(targetsByKey.get(target)?.dependsOn ?? []));
    }

    // A target the user ticked directly stays explicit, so its checkbox stays unlocked.
    for (const target of explicitTargets) {
      implied.delete(target);
    }

    return implied;
  }, [explicitTargets, targetsByKey]);

  // Sent in the catalogue's own order so the request reads the way the dialog does. The server sorts it
  // into dependency order regardless.
  const submitTargets = useMemo(() => {
    return targets
      .filter((target) => explicitTargets.has(target.target) || impliedTargets.has(target.target))
      .map((target) => target.target);
  }, [targets, explicitTargets, impliedTargets]);

  // Which explicit ticks are (transitively) responsible for an implied target, so its row can say so.
  const requiredByNames = useCallback(
    (target: SeedTarget): string[] => {
      const names: string[] = [];

      for (const candidate of targets) {
        if (explicitTargets.has(candidate.target) === false) {
          continue;
        }

        if (dependsOnTransitively(candidate.target, target, targetsByKey)) {
          names.push(candidate.displayName);
        }
      }

      return names;
    },
    [targets, explicitTargets, targetsByKey],
  );

  const toggle = useCallback((target: SeedTarget) => {
    setExplicitTargets((previous) => {
      const next = new Set(previous);

      if (next.has(target)) {
        next.delete(target);
      } else {
        next.add(target);
      }

      return next;
    });
  }, []);

  const selectAll = useCallback(() => {
    setExplicitTargets(new Set(targets.map((target) => target.target)));
  }, [targets]);

  const clear = useCallback(() => {
    setExplicitTargets(new Set());
  }, []);

  return { explicitTargets, impliedTargets, submitTargets, requiredByNames, toggle, selectAll, clear };
}

/**
 * Whether `from` reaches `to` by following dependsOn edges. Used only to explain a locked row, so it walks
 * the graph fresh each call rather than caching — the graph is seven nodes deep at most.
 */
function dependsOnTransitively(
  from: SeedTarget,
  to: SeedTarget,
  targetsByKey: Map<SeedTarget, SeedTargetInfo>,
): boolean {
  const visited = new Set<SeedTarget>();
  const pending = [...(targetsByKey.get(from)?.dependsOn ?? [])];

  while (pending.length > 0) {
    const target = pending.pop()!;

    if (target === to) {
      return true;
    }

    if (visited.has(target)) {
      continue;
    }

    visited.add(target);
    pending.push(...(targetsByKey.get(target)?.dependsOn ?? []));
  }

  return false;
}
