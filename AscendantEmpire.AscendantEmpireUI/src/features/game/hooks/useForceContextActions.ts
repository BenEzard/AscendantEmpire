import { useQuery } from "@tanstack/react-query";
import { getForceContextActions, type ForceContextAction } from "../api/forceContextActionsApi";

export const forceContextActionsKey = (unitInstanceId: number | undefined) => ["forceContextActions", unitInstanceId] as const;

/**
 * Fetches the context actions available to the selected force.
 *
 * Keyed per force, so clicking between forces stacked on a tile serves each from cache rather than refetching.
 * The result reflects the force's movement and strength at the time it was fetched, so anything that changes
 * either — a move, a split, ending the turn — must invalidate this key.
 */
export function useForceContextActionsQuery(unitInstanceId: number | undefined) {
  return useQuery<ForceContextAction[], Error>({
    queryKey: forceContextActionsKey(unitInstanceId),
    enabled: unitInstanceId != null,
    refetchOnWindowFocus: false,
    queryFn: async () => {
      if (unitInstanceId == null) throw new Error("unitInstanceId is null");

      return await getForceContextActions(unitInstanceId);
    },
  });
}
