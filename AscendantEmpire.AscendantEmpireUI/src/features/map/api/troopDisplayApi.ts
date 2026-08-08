import { http } from "@/api/http";

export interface TroopDisplayInfoDto {
  unitTemplateId: number;
  mapImagePath: string;
}

type TroopDisplayInfoWireDto = Partial<{
  unitTemplateId: number;
  mapImagePath: string;
}>;

type TroopDisplayResponseWireDto = Partial<{
  troopDisplayInfo: TroopDisplayInfoWireDto[];
}>;

function normalizeTroopDisplayInfo(info: TroopDisplayInfoWireDto): TroopDisplayInfoDto {
  return {
    unitTemplateId: info.unitTemplateId ?? 0,
    mapImagePath: info.mapImagePath ?? "",
  };
}

// No gameId argument: the server takes the game from the token, so this always returns the entered game's units.
export async function getTroopDisplayInfo(): Promise<TroopDisplayInfoDto[]> {
  const { data } = await http.get<TroopDisplayResponseWireDto>("/units/display");
  return (data.troopDisplayInfo ?? []).map(normalizeTroopDisplayInfo);
}
