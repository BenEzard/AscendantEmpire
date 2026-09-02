import { useQuery } from "@tanstack/react-query";
import { getForceContextActions, type ForceContextAction } from "../api/forceContextActionsApi";

/**
 * The cache key for a selection.
 *
 * Sorted so that selecting the same forces in a different order hits the same entry — the answer depends on
 * which forces are selected, never on the order they were ctrl+clicked in. Joined into a string so the key is a
 * primitive: an array literal would be a fresh identity on every render.
 */
export const forceContextActionsKey = (forceIds: readonly number[]) =>
  ["forceContextActions", [...forceIds].sort((a, b) => a - b).join(",")] as const;

/**
 * Fetches the context actions available to the selected forces.
 *
 * Keyed per selection, so clicking between forces stacked on a tile serves each from cache rather than
 * refetching. The result reflects the forces' movement and strength at the time it was fetched, so anything
 * that changes either — a move, a split, ending the turn — must invalidate this key.
 */
export function useForceContextActionsQuery(forceIds: readonly number[]) {
  return useQuery<ForceContextAction[], Error>({
    queryKey: forceContextActionsKey(forceIds),
    enabled: forceIds.length > 0,
    refetchOnWindowFocus: false,
    queryFn: async () => {
      if (forceIds.length === 0) throw new Error("No forces are selected");

      return await getForceContextActions(forceIds);
    },
  });
}
