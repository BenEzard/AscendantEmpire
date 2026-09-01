import { http } from "@/api/http";

export interface UnitDisplayInfoDto {
  unitTemplateId: number;
  mapImagePath: string;
  // The template's name — "Settler", "Warrior". Not a name for any individual unit built from it.
  displayName: string;
}

type UnitDisplayInfoWireDto = Partial<{
  unitTemplateId: number;
  mapImagePath: string;
  displayName: string;
}>;

type UnitDisplayResponseWireDto = Partial<{
  unitDisplayInfo: UnitDisplayInfoWireDto[];
}>;

function normalizeUnitDisplayInfo(info: UnitDisplayInfoWireDto): UnitDisplayInfoDto {
  return {
    unitTemplateId: info.unitTemplateId ?? 0,
    mapImagePath: info.mapImagePath ?? "",
    displayName: info.displayName ?? "",
  };
}

// No gameId argument: the server takes the game from the token, so this always returns the entered game's units.
export async function getUnitDisplayInfo(): Promise<UnitDisplayInfoDto[]> {
  const { data } = await http.get<UnitDisplayResponseWireDto>("/units/display");
  return (data.unitDisplayInfo ?? []).map(normalizeUnitDisplayInfo);
}
