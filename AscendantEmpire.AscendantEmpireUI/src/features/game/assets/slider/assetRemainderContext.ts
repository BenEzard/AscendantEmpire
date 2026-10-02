import { createContext, useContext } from "react";

// ---- Consume-remainder context ----

export interface AssetRemainderContextValue {
  activeRemainderId: string | null;
  registerCandidate: (id: string, initialWill: boolean) => void;
  unregisterCandidate: (id: string) => void;
  requestRemainderToggle: (id: string) => void;
}

export const AssetRemainderContext = createContext<AssetRemainderContextValue | null>(null);

export function useAssetRemainder(): AssetRemainderContextValue | null {
  return useContext(AssetRemainderContext);
}
