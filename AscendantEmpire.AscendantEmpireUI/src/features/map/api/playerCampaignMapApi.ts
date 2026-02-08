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

export type PlayerStructureDto = {
  campaignMapTileId: number | null;
  battleMapTileId: number | null;
  xCoord: number;
  yCoord: number;
  structureId: number;
  name: string;
  code: string;
  completionPercent: number;
  ownerPlayerId: number;
  sightRange: number;
};

export type PlayerStructuresResponseDto = {
  structures: PlayerStructureDto[];
};

// Be tolerant of backend JSON casing (camelCase vs PascalCase).
type PlayerStructureWireDto = Partial<{
  campaignMapTileId: number | null;
  CampaignMapTileId: number | null;
  battleMapTileId: number | null;
  BattleMapTileId: number | null;
  xCoord: number;
  XCoord: number;
  yCoord: number;
  YCoord: number;
  structureId: number;
  StructureId: number;
  name: string;
  Name: string;
  code: string;
  Code: string;
  completionPercent: number;
  CompletionPercent: number;
  ownerPlayerId: number;
  OwnerPlayerId: number;
  sightRange: number;
  SightRange: number;
}>;

type PlayerStructuresResponseWireDto = Partial<{
  structures: PlayerStructureWireDto[];
  Structures: PlayerStructureWireDto[];
}>;

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

async function getPlayerStructures(gameGuid: string, playerGuid: string): Promise<PlayerStructureDto[]> {
  const baseUrl = http.defaults.baseURL ?? "";
  console.log("[API] GET /player/structures", { baseUrl, gameGuid, playerGuid });

  const { data } = await http.get<PlayerStructuresResponseWireDto>("/player/structures", {
    params: { gameGuid, playerGuid },
  });

  const structures = data.structures ?? data.Structures ?? [];

  return structures.map((s): PlayerStructureDto => {
    const campaignMapTileId = (s.campaignMapTileId ?? s.CampaignMapTileId ?? null) as number | null;
    const battleMapTileId = (s.battleMapTileId ?? s.BattleMapTileId ?? null) as number | null;

    return {
      campaignMapTileId,
      battleMapTileId,
      xCoord: (s.xCoord ?? s.XCoord ?? 0) as number,
      yCoord: (s.yCoord ?? s.YCoord ?? 0) as number,
      structureId: (s.structureId ?? s.StructureId ?? 0) as number,
      name: (s.name ?? s.Name ?? "") as string,
      code: (s.code ?? s.Code ?? "") as string,
      completionPercent: (s.completionPercent ?? s.CompletionPercent ?? 0) as number,
      ownerPlayerId: (s.ownerPlayerId ?? s.OwnerPlayerId ?? 0) as number,
      sightRange: (s.sightRange ?? s.SightRange ?? 0) as number,
    };
  });
}

function structuresToObservers(structures: PlayerStructureDto[]): PlayerCampaignMapObserverDto[] {
  return structures.map((s, i) => {
    const temporaryObserverId = s.campaignMapTileId ?? s.battleMapTileId ?? s.structureId ?? i + 1;
    return {
      temporaryObserverId,
      location: { x: s.xCoord, y: s.yCoord },
      sightRange: s.sightRange ?? 0,
    };
  });
}

export async function postPlayerCampaignMap(req: LoadPlayerCampaignMapRequest): Promise<CampaignMapTilesResponse> {
  const baseUrl = http.defaults.baseURL ?? "";
  let normalizedReq: LoadPlayerCampaignMapRequest = req;

  // Observers now come from the server-side player structures endpoint.
  // If it fails (e.g. endpoint not available yet), fall back to whatever the caller provided.
  try {
    const structures = await getPlayerStructures(req.gameGuid, req.playerGuid);
    normalizedReq = {
      ...req,
      observers: structuresToObservers(structures),
    };
  } catch (e) {
    console.warn("[API] GET /player/structures failed; falling back to provided observers", e);
  }

  console.log("[API] POST /campaign-map/player", { baseUrl, req: normalizedReq });

  const { data } = await http.post<PlayerCampaignMapResponseDto>("/campaign-map/player", normalizedReq, {
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
