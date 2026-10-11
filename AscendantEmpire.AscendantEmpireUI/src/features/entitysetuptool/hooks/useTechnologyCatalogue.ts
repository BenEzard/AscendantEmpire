import { useQuery } from "@tanstack/react-query";
import { getTechnologyCatalogue } from "../api/technologyApi";

/** The react-query key the technology catalogue is cached under. Exported so anything that ever gains a write path can invalidate it. */
export const technologyCatalogueKey = ["technology", "catalogue"] as const;

export interface UseTechnologyCatalogueOptions {
  /**
   * Set false to skip the fetch entirely. The tree viewer accepts an injected catalogue for tests, and this is how it still calls the hook unconditionally
   * rather than behind a condition React's rules forbid.
   */
  enabled?: boolean;
}

/**
 * The whole technology catalogue, fetched once.
 *
 * Held indefinitely rather than on the page's shared 60-second staleTime: the read is player-independent while technology customisation is Identical, and
 * nothing in this tool can edit the catalogue, so it cannot go stale while the viewer is open. There is no mutation counterpart — the viewer is read-only.
 */
export function useTechnologyCatalogue(options?: UseTechnologyCatalogueOptions) {
  return useQuery({
    queryKey: technologyCatalogueKey,
    queryFn: ({ signal }) => getTechnologyCatalogue(signal),
    staleTime: Infinity,
    enabled: options?.enabled ?? true,
  });
}
