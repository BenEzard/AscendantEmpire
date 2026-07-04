import { createContext, useContext } from "react";

// ---- Consume-remainder context ----

export interface ResourceRemainderContextValue {
  activeRemainderId: string | null;
  registerCandidate: (id: string, initialWill: boolean) => void;
  unregisterCandidate: (id: string) => void;
  requestRemainderToggle: (id: string) => void;
}

export const ResourceRemainderContext = createContext<ResourceRemainderContextValue | null>(null);

export function useResourceRemainder(): ResourceRemainderContextValue | null {
  return useContext(ResourceRemainderContext);
}
