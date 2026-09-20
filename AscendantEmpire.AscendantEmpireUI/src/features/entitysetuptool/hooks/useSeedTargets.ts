import { useQuery } from "@tanstack/react-query";
import { getSeedTargets, type SeedTargetInfo } from "../api/seedApi";

export interface UseSeedTargetsResult {
  /** The seed targets, already ordered by group then sort order. Empty until the first load finishes. */
  targets: SeedTargetInfo[];
  isLoading: boolean;
  /** Populated when the catalogue could not be fetched; the dialog has nothing to show in that case. */
  errorMessage: string | null;
}

/**
 * Loads the seed target catalogue that backs the selection dialog.
 *
 * The catalogue is derived from the backend's registered seed steps and cannot change while the page is
 * open, so it is fetched once and never refetched — `staleTime: Infinity` rather than a polling interval.
 */
export function useSeedTargets(): UseSeedTargetsResult {
  const query = useQuery<SeedTargetInfo[]>({
    queryKey: ["system/seed/targets"],
    queryFn: ({ signal }) => getSeedTargets(signal),
    staleTime: Infinity,
  });

  let errorMessage: string | null = null;

  if (query.error) {
    errorMessage = query.error instanceof Error ? query.error.message : "Failed to load the seed targets.";
  }

  return {
    targets: query.data ?? [],
    isLoading: query.isLoading,
    errorMessage,
  };
}
