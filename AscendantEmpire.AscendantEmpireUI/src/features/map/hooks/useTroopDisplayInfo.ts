import { useQuery } from "@tanstack/react-query";
import { getTroopDisplayInfo } from "@/features/map/api/troopDisplayApi";
import { buildTroopImageSrcByUnitTemplateId } from "@/features/map/util/troopDisplayImageResolver";

export const troopDisplayInfoKey = (gameId: number | undefined) => ["troopDisplayInfo", gameId] as const;

export function useTroopDisplayInfoQuery(gameId: number | undefined, enabled: boolean) {
  return useQuery<Map<number, string>, Error>({
    queryKey: troopDisplayInfoKey(gameId),
    enabled: enabled && gameId != null,
    refetchOnWindowFocus: false,
    queryFn: async () => {
      if (gameId == null) throw new Error("gameId is null");

      // gameId is only the cache key here — the request itself carries no game, because the server reads it from the token.
      const troopDisplayInfo = await getTroopDisplayInfo();
      return buildTroopImageSrcByUnitTemplateId(troopDisplayInfo);
    },
  });
}