import { useQuery } from "@tanstack/react-query";
import { getTroopDisplayInfo } from "@/features/map/api/troopDisplayApi";
import { buildTroopImageSrcByUnitDesignId } from "@/features/map/util/troopDisplayImageResolver";

export const troopDisplayInfoKey = (gameId: number | undefined) => ["troopDisplayInfo", gameId] as const;

export function useTroopDisplayInfoQuery(gameId: number | undefined, enabled: boolean) {
  return useQuery<Map<number, string>, Error>({
    queryKey: troopDisplayInfoKey(gameId),
    enabled: enabled && gameId != null,
    refetchOnWindowFocus: false,
    queryFn: async () => {
      if (gameId == null) throw new Error("gameId is null");
      const troopDisplayInfo = await getTroopDisplayInfo(gameId);
      return buildTroopImageSrcByUnitDesignId(troopDisplayInfo);
    },
  });
}