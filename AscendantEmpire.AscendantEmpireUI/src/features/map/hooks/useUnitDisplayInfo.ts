import { useQuery } from "@tanstack/react-query";
import { getUnitDisplayInfo } from "@/features/map/api/unitDisplayApi";
import { buildUnitImageSrcByUnitTemplateId } from "@/features/map/util/unitDisplayImageResolver";

export const unitDisplayInfoKey = (gameId: number | undefined) => ["unitDisplayInfo", gameId] as const;

export function useUnitDisplayInfoQuery(gameId: number | undefined, enabled: boolean) {
  return useQuery<Map<number, string>, Error>({
    queryKey: unitDisplayInfoKey(gameId),
    enabled: enabled && gameId != null,
    refetchOnWindowFocus: false,
    queryFn: async () => {
      if (gameId == null) throw new Error("gameId is null");

      // gameId is only the cache key here — the request itself carries no game, because the server reads it from the token.
      const unitDisplayInfo = await getUnitDisplayInfo();
      return buildUnitImageSrcByUnitTemplateId(unitDisplayInfo);
    },
  });
}