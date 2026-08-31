import { useQuery } from "@tanstack/react-query";
import { getTroopContextActions, type TroopContextAction } from "../api/troopContextActionsApi";

export const troopContextActionsKey = (troopInstanceId: number | undefined) => ["troopContextActions", troopInstanceId] as const;

/**
 * Fetches the context actions available to the selected troop.
 *
 * Keyed per troop, so clicking between troops stacked on a tile serves each from cache rather than refetching.
 * The result reflects the troop's movement and strength at the time it was fetched, so anything that changes
 * either — a move, a split, ending the turn — must invalidate this key.
 */
export function useTroopContextActionsQuery(troopInstanceId: number | undefined) {
  return useQuery<TroopContextAction[], Error>({
    queryKey: troopContextActionsKey(troopInstanceId),
    enabled: troopInstanceId != null,
    refetchOnWindowFocus: false,
    queryFn: async () => {
      if (troopInstanceId == null) throw new Error("troopInstanceId is null");

      return await getTroopContextActions(troopInstanceId);
    },
  });
}
