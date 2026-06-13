import { http } from "@/api/http";

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------

export type StartPositionDto = {
  xCoord: number;
  yCoord: number;
  displayName: string | null;
  locationScore: number | null;
};

export type StartPositionResponseItem = {
  id: number;
  campaignMapGalleryIndexId: number;
  xCoord: number;
  yCoord: number;
  displayName: string | null;
  locationScore: number | null;
};

export type StartPositionsResponse = {
  campaignMapGalleryIndexId: number;
  startPositions: StartPositionResponseItem[];
};

export type PutStartPositionsDto = {
  startPositions: StartPositionDto[];
};

// ---------------------------------------------------------------------------
// Wire normalisation (backend may return PascalCase or camelCase)
// ---------------------------------------------------------------------------

type StartPositionResponseItemWire = Partial<{
  id: number;
  Id: number;
  campaignMapGalleryIndexId: number;
  CampaignMapGalleryIndexId: number;
  xCoord: number;
  XCoord: number;
  yCoord: number;
  YCoord: number;
  displayName: string | null;
  DisplayName: string | null;
  locationScore: number | null;
  LocationScore: number | null;
}>;

type StartPositionsResponseWire = Partial<{
  campaignMapGalleryIndexId: number;
  CampaignMapGalleryIndexId: number;
  startPositions: StartPositionResponseItemWire[];
  StartPositions: StartPositionResponseItemWire[];
}>;

function normalizeItem(item: StartPositionResponseItemWire): StartPositionResponseItem {
  return {
    id: (item.id ?? item.Id ?? 0) as number,
    campaignMapGalleryIndexId: (item.campaignMapGalleryIndexId ?? item.CampaignMapGalleryIndexId ?? 0) as number,
    xCoord: (item.xCoord ?? item.XCoord ?? 0) as number,
    yCoord: (item.yCoord ?? item.YCoord ?? 0) as number,
    displayName: (item.displayName ?? item.DisplayName ?? null) as string | null,
    locationScore: (item.locationScore ?? item.LocationScore ?? null) as number | null,
  };
}

function normalizeResponse(data: StartPositionsResponseWire): StartPositionsResponse {
  const campaignMapGalleryIndexId = (data.campaignMapGalleryIndexId ?? data.CampaignMapGalleryIndexId ?? 0) as number;
  const itemsWire = (data.startPositions ?? data.StartPositions ?? []) as StartPositionResponseItemWire[];
  return {
    campaignMapGalleryIndexId,
    startPositions: itemsWire.map(normalizeItem),
  };
}

// ---------------------------------------------------------------------------
// API functions
// ---------------------------------------------------------------------------

export async function getCampaignMapStartPositions(
  campaignMapGalleryIndexId: number,
  signal?: AbortSignal,
): Promise<StartPositionsResponse> {
  const { data } = await http.get<StartPositionsResponseWire>(
    `/map-gallery/${campaignMapGalleryIndexId}/start-positions`,
    { signal },
  );
  return normalizeResponse(data);
}

export async function putCampaignMapStartPositions(
  campaignMapGalleryIndexId: number,
  dto: PutStartPositionsDto,
  signal?: AbortSignal,
): Promise<StartPositionsResponse> {
  const { data } = await http.put<StartPositionsResponseWire>(
    `/map-gallery/${campaignMapGalleryIndexId}/start-positions`,
    dto,
    {
      signal,
      headers: { "Content-Type": "application/json" },
    },
  );
  return normalizeResponse(data);
}
