import { http } from "@/api/http";

export type PlayerCampaignMapObserverDto = {
  temporaryObserverId: number;
  location: {
    x: number;
    y: number;
  };
  sightRange: number;
};

export type LoadPlayerCampaignMapRequest = {
  gameGuid: string;
  playerGuid: string;
  loadWholeMap?: boolean;
  observers?: PlayerCampaignMapObserverDto[];
};

export type ClearPlayerCampaignMapRequest = {
  gameGuid: string;
  playerGuid: string;
};

export interface CampaignMapTileDto {
  xCoord: number;
  yCoord: number;
  biomeTypeId: number;
  campaignMapGeographyTypeId: number;
}

export type PlayerCampaignMapObserverResultDto = {
  temporaryObserverId: number;
  xCoord: number;
  yCoord: number;
  seenTiles: CampaignMapTileDto[];
};

// The API currently returns observers with their seen tiles.
// Keep this separate from the normalized UI-facing shape below.
export type PlayerCampaignMapResponseDto = {
  observers: PlayerCampaignMapObserverResultDto[];
  // Back-compat: if the API ever returns a flat list, we can still handle it.
  tiles?: CampaignMapTileDto[];
};

export interface CampaignMapTilesResponse {
  tiles: CampaignMapTileDto[];
  // Some endpoints also return this; keep optional so partial loads work.
  campaignMapGalleryIndexId?: number;
  // Optional for callers that care about observer metadata.
  observers?: PlayerCampaignMapObserverResultDto[];
}

export async function postPlayerCampaignMap(req: LoadPlayerCampaignMapRequest): Promise<CampaignMapTilesResponse> {
  const baseUrl = http.defaults.baseURL ?? "";
  console.log("[API] POST /campaign-map/player", { baseUrl, req });

  const { data } = await http.post<PlayerCampaignMapResponseDto>("/campaign-map/player", req, {
    headers: { "Content-Type": "application/json" },
  });

  // Normalize to the flat tile list the rest of the UI expects.
  const tilesFromObservers = (data.observers ?? []).flatMap((o) => o.seenTiles ?? []);
  const tiles = (data.tiles ?? tilesFromObservers) ?? [];

  return {
    tiles,
    observers: data.observers,
  };
}

export async function postClearPlayerCampaignMap(req: ClearPlayerCampaignMapRequest): Promise<void> {
  const baseUrl = http.defaults.baseURL ?? "";
  console.log("[API] POST /campaign-map/player/clear", { baseUrl, req });

  await http.post("/campaign-map/player/clear", req, {
    headers: { "Content-Type": "application/json" },
  });
}
