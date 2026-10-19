import { http } from "@/api/http";

export interface StructureDisplayInfoDto {
  structureId: number;
  /**
   * The kind's stable catalogue code, e.g. "village".
   *
   * What the artwork is keyed on. The live map read sends a code on each observed structure, but a last-known-structure
   * marker carries only an id, so this catalogue is how that marker resolves the kind's art.
   */
  code: string;
  /** The kind's name — "Watchtower", "Village". Not a name for any individual structure of that kind. */
  name: string;
  mapImagePath: string;
}

type StructureDisplayInfoWireDto = Partial<StructureDisplayInfoDto>;

type StructureDisplayResponseWireDto = Partial<{
  structureDisplayInfo: StructureDisplayInfoWireDto[];
}>;

function normalizeStructureDisplayInfo(info: StructureDisplayInfoWireDto): StructureDisplayInfoDto {
  return {
    structureId: info.structureId ?? 0,
    code: info.code ?? "",
    name: info.name ?? "",
    mapImagePath: info.mapImagePath ?? "",
  };
}

/** Every kind of structure the campaign map can show. The catalogue is global, so nothing about the game is sent or needed. */
export async function getStructureDisplayInfo(): Promise<StructureDisplayInfoDto[]> {
  const { data } = await http.get<StructureDisplayResponseWireDto>("/structures/display");

  return (data.structureDisplayInfo ?? []).map(normalizeStructureDisplayInfo);
}
