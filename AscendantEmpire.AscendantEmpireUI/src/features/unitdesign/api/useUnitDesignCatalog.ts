// Loads the full, hydrated unit-design catalogue for the designer: every body + component, each fetched
// in full (the catalog list rows are lossy — no slots / stats). Bodies are needed as bases and carry the
// slot layout, so we hydrate everything up front.
//
// N+1 requests (one list + one per component) is acceptable for the current dev-scale catalogue; if the
// catalogue grows, add a bulk "full catalog" endpoint and swap the per-code fetch out here.

import { useEffect, useState } from "react";

import { getComponentList } from "./catalogApi";
import { getComponent } from "./componentsApi";
import type { UnitComponent } from "../types/UnitComponent";

export interface UnitDesignCatalogState {
  /** Fully-hydrated components (bodies + non-body components). Empty until the load resolves. */
  catalog: UnitComponent[];
  loading: boolean;
  /** A user-facing message if the load failed, else null. */
  error: string | null;
}

/**
 * Fetches and hydrates the whole catalogue once on mount (when `enabled`). Pass `enabled: false` to skip
 * the fetch entirely — e.g. when the host injects its own catalogue for tests.
 */
export function useUnitDesignCatalog(enabled: boolean = true): UnitDesignCatalogState {
  const [catalog, setCatalog] = useState<UnitComponent[]>([]);
  const [loading, setLoading] = useState<boolean>(enabled);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!enabled) {
      return;
    }

    // Guards against setting state after unmount (the load is a multi-request async chain).
    let cancelled = false;

    setLoading(true);
    setError(null);

    (async () => {
      try {
        const rows = await getComponentList();
        // Hydrate each row to a full component; a single failure fails the whole load (reported below).
        const hydrated = await Promise.all(rows.map((row) => getComponent(row.code)));
        if (!cancelled) {
          setCatalog(hydrated);
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : "Failed to load the unit-design catalogue.");
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [enabled]);

  return { catalog, loading, error };
}
