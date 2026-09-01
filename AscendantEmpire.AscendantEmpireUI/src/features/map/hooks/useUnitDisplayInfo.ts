import { useQuery } from "@tanstack/react-query";
import { getUnitDisplayInfo } from "@/features/map/api/unitDisplayApi";
import { buildUnitImageSrcByUnitTemplateId } from "@/features/map/util/unitDisplayImageResolver";

export const unitDisplayInfoKey = (gameId: number | undefined) => ["unitDisplayInfo", gameId] as const;

/**
 * What one call to /units/display yields, keyed by unit template id.
 *
 * Two maps rather than one map of pairs, because the two are wanted in different places: the map grid needs only
 * images, and the force selection bar needs only names. Neither has to carry the other's payload around.
 */
export interface UnitDisplayLookup {
  imageSrcByUnitTemplateId: Map<number, string>;
  nameByUnitTemplateId: Map<number, string>;
}

export function useUnitDisplayInfoQuery(gameId: number | undefined, enabled: boolean) {
  return useQuery<UnitDisplayLookup, Error>({
    queryKey: unitDisplayInfoKey(gameId),
    enabled: enabled && gameId != null,
    refetchOnWindowFocus: false,
    queryFn: async () => {
      if (gameId == null) throw new Error("gameId is null");

      // gameId is only the cache key here — the request itself carries no game, because the server reads it from the token.
      const unitDisplayInfo = await getUnitDisplayInfo();

      return {
        // Images need resolving against the bundled assets; names arrive ready to use.
        imageSrcByUnitTemplateId: buildUnitImageSrcByUnitTemplateId(unitDisplayInfo),
        nameByUnitTemplateId: new Map(unitDisplayInfo.filter((info) => info.displayName).map((info) => [info.unitTemplateId, info.displayName])),
      };
    },
  });
}
