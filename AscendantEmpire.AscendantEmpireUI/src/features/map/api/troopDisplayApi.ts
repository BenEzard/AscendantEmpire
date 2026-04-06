import { http } from "@/api/http";

export interface TroopDisplayInfoDto {
  unitDesignId: number;
  mapImagePath: string;
}

type TroopDisplayInfoWireDto = Partial<{
  unitDesignId: number;
  mapImagePath: string;
}>;

type TroopDisplayResponseWireDto = Partial<{
  troopDisplayInfo: TroopDisplayInfoWireDto[];
}>;

function normalizeTroopDisplayInfo(info: TroopDisplayInfoWireDto): TroopDisplayInfoDto {
  return {
    unitDesignId: info.unitDesignId ?? 0,
    mapImagePath: info.mapImagePath ?? "",
  };
}

export async function getTroopDisplayInfo(gameId: number): Promise<TroopDisplayInfoDto[]> {
  const { data } = await http.get<TroopDisplayResponseWireDto>(`/units/${gameId}/display`);
  return (data.troopDisplayInfo ?? []).map(normalizeTroopDisplayInfo);
}