import { createContext, useContext } from "react";
import type { LockedState } from "./Slider";

export interface SliderEntry {
  value: number;
  lockState: LockedState;
}

export interface CollectionState {
  /** Insertion-ordered list of slider IDs — determines "first slider" for remainder distribution. */
  order: string[];
  entries: Record<string, SliderEntry>;
}

export interface SliderCollectionContextValue {
  register: (id: string, initialValue: number, initialLockState: LockedState) => void;
  unregister: (id: string) => void;
  getEntry: (id: string) => SliderEntry | undefined;
  requestValueUpdate: (id: string, requestedValue: number) => void;
  requestLockToggle: (id: string) => void;
}

export const SliderCollectionContext = createContext<SliderCollectionContextValue | null>(null);

export function useSliderCollection(): SliderCollectionContextValue | null {
  return useContext(SliderCollectionContext);
}

/**
 * Rebalances the collection after `changedId` requests a new value.
 *
 * Rules:
 * - The changed slider is capped at `100 - lockedTotal` so locked sliders are
 *   never touched.
 * - If the new total would exceed 100, the excess is distributed equally across
 *   the other *unlocked* sliders (excluding the changed one). Any non-divisible
 *   remainder is taken from the first slider in insertion order.
 * - Multi-pass: if a slider hits 0 before absorbing its share, the leftover is
 *   spread across the remaining reducible sliders.
 */
export function rebalance(state: CollectionState, changedId: string, requestedValue: number): CollectionState {
  const { order, entries } = state;

  const lockedTotal = order.filter((id) => entries[id]?.lockState !== "unlocked").reduce((sum, id) => sum + (entries[id]?.value ?? 0), 0);

  const otherUnlockedIds = order.filter((id) => id !== changedId && entries[id]?.lockState === "unlocked");
  const otherUnlockedTotal = otherUnlockedIds.reduce((sum, id) => sum + (entries[id]?.value ?? 0), 0);

  // Cap: locked sliders are immovable, and other unlocked sliders can floor at 0.
  const cappedValue = Math.max(0, Math.min(requestedValue, 100 - lockedTotal));

  const excess = lockedTotal + cappedValue + otherUnlockedTotal - 100;

  const newChangedEntry: SliderEntry = { ...entries[changedId], value: cappedValue };

  if (excess <= 0) {
    // Plenty of unallocated room — no rebalancing needed.
    return { ...state, entries: { ...entries, [changedId]: newChangedEntry } };
  }

  // Distribute `excess` equally across other unlocked sliders.
  const reduced = distributeReduction(
    otherUnlockedIds.map((id) => ({ id, value: entries[id].value })),
    excess,
  );

  const newEntries: Record<string, SliderEntry> = { ...entries, [changedId]: newChangedEntry };
  for (const { id, value } of reduced) {
    newEntries[id] = { ...newEntries[id], value };
  }
  return { ...state, entries: newEntries };
}

/**
 * Reduces `totalToReduce` from `sliders` as equally as possible.
 * Remainder (when not evenly divisible) is taken from the first slider.
 * Multi-pass: sliders that hit 0 are excluded from subsequent passes.
 */
function distributeReduction(sliders: Array<{ id: string; value: number }>, totalToReduce: number): Array<{ id: string; value: number }> {
  const result = sliders.map((s) => ({ ...s }));
  let remaining = totalToReduce;

  while (remaining > 0) {
    // Sort descending by value so the slider with the most room gets any
    // non-divisible remainder. This ensures the burden rotates fairly across
    // sliders on successive drag steps rather than always burdening the first.
    const reducible = result.filter((r) => r.value > 0).sort((a, b) => b.value - a.value);
    if (reducible.length === 0) break;

    const n = reducible.length;
    const base = Math.floor(remaining / n);
    const rem = remaining % n;

    let applied = 0;
    reducible.forEach((r, i) => {
      const share = base + (i === 0 ? rem : 0);
      const actual = Math.min(share, r.value);
      r.value -= actual;
      applied += actual;
    });

    remaining -= applied;
    if (applied === 0) break; // safety: nothing could be reduced
  }

  return result;
}
