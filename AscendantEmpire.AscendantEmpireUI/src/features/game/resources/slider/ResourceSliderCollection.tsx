import { useState, useCallback, useMemo, useEffect, createContext, useContext, type ReactNode } from "react";
import type { LockedState } from "../../../../components/ui/slider/Slider";
import { SliderCollectionContext, rebalance } from "../../../../components/ui/slider/SliderCollectionContext";
import type { CollectionState, SliderEntry, SliderSnapshot } from "../../../../components/ui/slider/SliderCollectionContext";

export type { SliderSnapshot };

// ---- Consume-remainder context ----

interface ResourceRemainderContextValue {
  activeRemainderId: string | null;
  registerCandidate: (id: string, initialWill: boolean) => void;
  unregisterCandidate: (id: string) => void;
  requestRemainderToggle: (id: string) => void;
}

const ResourceRemainderContext = createContext<ResourceRemainderContextValue | null>(null);

export function useResourceRemainder(): ResourceRemainderContextValue | null {
  return useContext(ResourceRemainderContext);
}

// ---- Collection props ----

export interface ResourceSliderCollectionProps {
  resourcesAddedPerTurn: number;
  children: ReactNode;
  onChange?: (sliders: SliderSnapshot[]) => void;
  /** Fired when the slider that will consume the allocation remainder changes. Receives the numeric slider id, or null if none is selected. */
  onRemainderChange?: (sliderId: number | null) => void;
}

export default function ResourceSliderCollection({ children, onChange, onRemainderChange }: ResourceSliderCollectionProps) {
  const [state, setState] = useState<CollectionState>({ order: [], entries: {} });

  type RemainderState = { candidates: string[]; activeId: string | null };
  const [remainderState, setRemainderState] = useState<RemainderState>({ candidates: [], activeId: null });

  useEffect(() => {
    if (!onRemainderChange) return;
    const numericId = remainderState.activeId !== null ? parseInt(remainderState.activeId, 10) : null;
    onRemainderChange(numericId);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [remainderState.activeId]);

  useEffect(() => {
    if (!onChange) return;
    const snapshots: SliderSnapshot[] = state.order.map((id) => {
      const entry = state.entries[id];
      return { id: entry.numericId, code: entry.code, value: entry.value, lockState: entry.lockState };
    });
    onChange(snapshots);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [state]);

  const register = useCallback((id: string, numericId: number, code: string, initialValue: number, initialLockState: LockedState) => {
    setState((prev) => {
      if (prev.entries[id]) return prev;
      return {
        order: [...prev.order, id],
        entries: { ...prev.entries, [id]: { numericId, code, value: initialValue, lockState: initialLockState } },
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
      if (!entry || entry.lockState === "systemlocked" || entry.lockState === "systemlockedinvisible" || entry.lockState === "unlockable")
        return prev;
      const newLockState: LockedState = entry.lockState === "unlocked" ? "locked" : "unlocked";
      return { ...prev, entries: { ...prev.entries, [id]: { ...entry, lockState: newLockState } } };
    });
  }, []);

  const contextValue = useMemo(
    () => ({ register, unregister, getEntry, requestValueUpdate, requestLockToggle }),
    [register, unregister, getEntry, requestValueUpdate, requestLockToggle],
  );

  const registerCandidate = useCallback((id: string, initialWill: boolean) => {
    setRemainderState((prev) => {
      if (prev.candidates.includes(id)) return prev;
      const candidates = [...prev.candidates, id];
      let activeId = prev.activeId;
      if (activeId === null && (candidates.length === 1 || initialWill)) {
        activeId = id;
      }
      return { candidates, activeId };
    });
  }, []);

  const unregisterCandidate = useCallback((id: string) => {
    setRemainderState((prev) => {
      const candidates = prev.candidates.filter((c) => c !== id);
      let activeId = prev.activeId === id ? null : prev.activeId;
      if (activeId === null && candidates.length === 1) {
        activeId = candidates[0];
      }
      return { candidates, activeId };
    });
  }, []);

  const requestRemainderToggle = useCallback((id: string) => {
    setRemainderState((prev) => {
      if (prev.candidates.length === 1) return prev; // single candidate cannot be deselected
      const activeId = prev.activeId === id ? null : id;
      return { ...prev, activeId };
    });
  }, []);

  const remainderContextValue = useMemo(
    () => ({ activeRemainderId: remainderState.activeId, registerCandidate, unregisterCandidate, requestRemainderToggle }),
    [remainderState.activeId, registerCandidate, unregisterCandidate, requestRemainderToggle],
  );

  return (
    <SliderCollectionContext.Provider value={contextValue}>
      <ResourceRemainderContext.Provider value={remainderContextValue}>
        <div className="flex flex-col gap-2">{children}</div>
      </ResourceRemainderContext.Provider>
    </SliderCollectionContext.Provider>
  );
}
