import { http } from "@/api/http";

export interface UnitDisplayInfoDto {
  unitTemplateId: number;
  mapImagePath: string;
}

type UnitDisplayInfoWireDto = Partial<{
  unitTemplateId: number;
  mapImagePath: string;
}>;

type UnitDisplayResponseWireDto = Partial<{
  unitDisplayInfo: UnitDisplayInfoWireDto[];
}>;

function normalizeUnitDisplayInfo(info: UnitDisplayInfoWireDto): UnitDisplayInfoDto {
  return {
    unitTemplateId: info.unitTemplateId ?? 0,
    mapImagePath: info.mapImagePath ?? "",
  };
}

// No gameId argument: the server takes the game from the token, so this always returns the entered game's units.
export async function getUnitDisplayInfo(): Promise<UnitDisplayInfoDto[]> {
  const { data } = await http.get<UnitDisplayResponseWireDto>("/units/display");
  return (data.unitDisplayInfo ?? []).map(normalizeUnitDisplayInfo);
}
