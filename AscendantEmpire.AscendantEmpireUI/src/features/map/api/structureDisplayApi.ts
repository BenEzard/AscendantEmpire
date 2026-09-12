import { http } from "@/api/http";

export interface StructureDisplayInfoDto {
  structureId: number;
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
    name: info.name ?? "",
    mapImagePath: info.mapImagePath ?? "",
  };
}

/** Every kind of structure the campaign map can show. The catalogue is global, so nothing about the game is sent or needed. */
export async function getStructureDisplayInfo(): Promise<StructureDisplayInfoDto[]> {
  const { data } = await http.get<StructureDisplayResponseWireDto>("/structures/display");

  return (data.structureDisplayInfo ?? []).map(normalizeStructureDisplayInfo);
}
