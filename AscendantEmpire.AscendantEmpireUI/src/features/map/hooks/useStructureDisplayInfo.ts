import { useQuery } from "@tanstack/react-query";
import { getStructureDisplayInfo } from "@/features/map/api/structureDisplayApi";

export const structureDisplayInfoKey = ["structureDisplayInfo"] as const;

/** What one call to /structures/display yields, keyed by structure id. */
export interface StructureDisplayLookup {
  nameByStructureId: Map<number, string>;
  /**
   * The catalogue code of each kind, which is what the artwork is keyed on.
   *
   * Needed only by the last-known-structure marker: a live structure arrives from the map read with its code already on it,
   * but a remembered one carries an id alone, so the marker has to come back here to find out which sprite to draw.
   */
  codeByStructureId: Map<number, string>;
}

/**
 * The names of every kind of structure, for intelligence sentences that only carry a structure id.
 *
 * Not keyed by game: the structure catalogue is the same for every game, so one fetch serves them all, and it is never refetched on its own because the
 * catalogue only changes when the game is updated.
 */
export function useStructureDisplayInfoQuery(enabled: boolean) {
  return useQuery<StructureDisplayLookup, Error>({
    queryKey: structureDisplayInfoKey,
    enabled,
    refetchOnWindowFocus: false,
    staleTime: Infinity,
    queryFn: async () => {
      const structureDisplayInfo = await getStructureDisplayInfo();

      return {
        nameByStructureId: new Map(structureDisplayInfo.filter((info) => info.name).map((info) => [info.structureId, info.name])),
        codeByStructureId: new Map(structureDisplayInfo.filter((info) => info.code).map((info) => [info.structureId, info.code])),
      };
    },
  });
}
