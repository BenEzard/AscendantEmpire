import { useState, useCallback, useMemo, type ReactNode } from "react";
import type { LockedState } from "./Slider";
import { SliderCollectionContext, rebalance } from "./SliderCollectionContext";
import type { CollectionState, SliderEntry } from "./SliderCollectionContext";

export interface SliderCollectionProps {
  resourcesAddedPerTurn: number;
  children: ReactNode;
}

export default function SliderCollection({ children }: SliderCollectionProps) {
  const [state, setState] = useState<CollectionState>({ order: [], entries: {} });

  const register = useCallback((id: string, initialValue: number, initialLockState: LockedState) => {
    setState((prev) => {
      if (prev.entries[id]) return prev;
      return {
        order: [...prev.order, id],
        entries: { ...prev.entries, [id]: { value: initialValue, lockState: initialLockState } },
      };
    });
  }, []);

  const unregister = useCallback((id: string) => {
    setState((prev) => {
      // eslint-disable-next-line @typescript-eslint/no-unused-vars
      const { [id]: _, ...rest } = prev.entries;
      return { order: prev.order.filter((x) => x !== id), entries: rest };
    });
  }, []);

  const getEntry = useCallback((id: string): SliderEntry | undefined => state.entries[id], [state]);

  const requestValueUpdate = useCallback((id: string, requestedValue: number) => {
    setState((prev) => rebalance(prev, id, requestedValue));
  }, []);

  const requestLockToggle = useCallback((id: string) => {
    setState((prev) => {
      const entry = prev.entries[id];
      if (!entry || entry.lockState === "systemlocked") return prev;
      const newLockState: LockedState = entry.lockState === "unlocked" ? "locked" : "unlocked";
      return { ...prev, entries: { ...prev.entries, [id]: { ...entry, lockState: newLockState } } };
    });
  }, []);

  const contextValue = useMemo(
    () => ({ register, unregister, getEntry, requestValueUpdate, requestLockToggle }),
    [register, unregister, getEntry, requestValueUpdate, requestLockToggle],
  );

  return (
    <SliderCollectionContext.Provider value={contextValue}>
      <div className="flex flex-col gap-2">{children}</div>
    </SliderCollectionContext.Provider>
  );
}
