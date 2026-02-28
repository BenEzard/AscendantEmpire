import { http } from "@/api/http";

export type CoordDto = {
  // Be tolerant to backend casing and potential naming.
  x?: number;
  y?: number;
  X?: number;
  Y?: number;
  xCoord?: number;
  yCoord?: number;
  XCoord?: number;
  YCoord?: number;
};

export type UpdateCampaignMapTilesDto = {
  coordinates: CoordDto[];
  biomeType?: number | null;
  geographyType?: number | null;
};

export type TileUpdateFailTile = {
  coord: { x: number; y: number };
  dbId: number;
  originalBiomeType: number;
  originalGeographyType: number;
};

export type TileGroupUpdateFail = {
  errorMessage: string;
  failedTiles: TileUpdateFailTile[];
};

export type UpdateCampaignMapTilesResponse = {
  tilesUpdated: number;
  failures: TileGroupUpdateFail[];
};

type UpdateCampaignMapTilesResponseWire = Partial<{
  tilesUpdated: number;
  TilesUpdated: number;
  failures: TileGroupUpdateFailWire[];
  Failures: TileGroupUpdateFailWire[];
}>;

type TileGroupUpdateFailWire = Partial<{
  errorMessage: string;
  ErrorMessage: string;
  failedTiles: TileUpdateFailTileWire[];
  FailedTiles: TileUpdateFailTileWire[];
}>;

type TileUpdateFailTileWire = Partial<{
  coord: CoordDto;
  Coord: CoordDto;
  dbId: number;
  DbId: number;
  originalBiomeType: number;
  OriginalBiomeType: number;
  originalGeographyType: number;
  OriginalGeographyType: number;
}>;

function normalizeCoord(coord: CoordDto | undefined | null): { x: number; y: number } {
  const x = (coord?.x ?? coord?.X ?? coord?.xCoord ?? coord?.XCoord ?? 0) as number;
  const y = (coord?.y ?? coord?.Y ?? coord?.yCoord ?? coord?.YCoord ?? 0) as number;
  return { x, y };
}

function normalizeResponse(data: UpdateCampaignMapTilesResponseWire): UpdateCampaignMapTilesResponse {
  const tilesUpdated = (data.tilesUpdated ?? data.TilesUpdated ?? 0) as number;
  const failuresWire = (data.failures ?? data.Failures ?? []) as TileGroupUpdateFailWire[];

  const failures: TileGroupUpdateFail[] = failuresWire.map((f) => {
    const errorMessage = (f.errorMessage ?? f.ErrorMessage ?? "") as string;
    const failedTilesWire = (f.failedTiles ?? f.FailedTiles ?? []) as TileUpdateFailTileWire[];

    const failedTiles: TileUpdateFailTile[] = failedTilesWire.map((t) => {
      const coordWire = (t.coord ?? t.Coord ?? {}) as CoordDto;
      return {
        coord: normalizeCoord(coordWire),
        dbId: (t.dbId ?? t.DbId ?? 0) as number,
        originalBiomeType: (t.originalBiomeType ?? t.OriginalBiomeType ?? 0) as number,
        originalGeographyType: (t.originalGeographyType ?? t.OriginalGeographyType ?? 0) as number,
      };
    });

    return { errorMessage, failedTiles };
  });

  return { tilesUpdated, failures };
}

export async function postUpdateCampaignMapTiles(
  campaignMapGalleryIndexId: number,
  dto: UpdateCampaignMapTilesDto,
  signal?: AbortSignal,
): Promise<UpdateCampaignMapTilesResponse> {
  const { data } = await http.post<UpdateCampaignMapTilesResponseWire>(
    `/map-gallery/${campaignMapGalleryIndexId}/update-tiles`,
    dto,
    {
      signal,
      headers: { "Content-Type": "application/json" },
    },
  );

  return normalizeResponse(data);
}
