import { http } from "@/api/http";

export type LoadPlayerCampaignMapRequest = {
  gameGuid: string;
  playerGuid: string;
  loadWholeMap?: boolean;
  observers?: null;
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
  structures?: ObservedStructureDto[];
  troops?: ObservedTroopDto[];
}

export type PlayerCampaignMapObserverResultDto = {
  id: number;
  observerRecordType: number;
  xCoord: number;
  yCoord: number;
  seenTiles: CampaignMapTileDto[];
};

export type ObservedStructureDto = {
  id: number;
  structureId: number;
  positionOnTile: number;
  completionPercent: number;
};

export type ObservedTroopDto = {
  id: number;
  recordType: number;
  unitDesignId: number;
};

type ObservedStructureWireDto = Partial<{
  id: number;
  Id: number;
  structureId: number;
  StructureId: number;
  positionOnTile: number;
  PositionOnTile: number;
  completionPercent: number;
  CompletionPercent: number;
}>;

type ObservedTroopWireDto = Partial<{
  id: number;
  Id: number;
  recordType: number;
  RecordType: number;
  unitDesignId: number;
  UnitDesignId: number;
}>;

type CampaignMapTileWireDto = Partial<{
  xCoord: number;
  XCoord: number;
  yCoord: number;
  YCoord: number;
  biomeTypeId: number;
  BiomeTypeId: number;
  campaignMapGeographyTypeId: number;
  CampaignMapGeographyTypeId: number;
  structures: ObservedStructureWireDto[];
  Structures: ObservedStructureWireDto[];
  troops: ObservedTroopWireDto[];
  Troops: ObservedTroopWireDto[];
}>;

type PlayerCampaignMapObserverResultWireDto = Partial<{
  id: number;
  Id: number;
  observerRecordType: number;
  ObserverRecordType: number;
  xCoord: number;
  XCoord: number;
  yCoord: number;
  YCoord: number;
  seenTiles: CampaignMapTileWireDto[];
  SeenTiles: CampaignMapTileWireDto[];
  temporaryObserverId: number;
  TemporaryObserverId: number;
}>;

// The API currently returns observers with their seen tiles.
// Keep this separate from the normalized UI-facing shape below.
export type PlayerCampaignMapResponseDto = {
  observers: PlayerCampaignMapObserverResultDto[];
  // Back-compat: if the API ever returns a flat list, we can still handle it.
  tiles?: CampaignMapTileDto[];
};

type PlayerCampaignMapResponseWireDto = Partial<{
  observers: PlayerCampaignMapObserverResultWireDto[];
  Observers: PlayerCampaignMapObserverResultWireDto[];
  tiles: CampaignMapTileWireDto[];
  Tiles: CampaignMapTileWireDto[];
}>;

export interface CampaignMapTilesResponse {
  tiles: CampaignMapTileDto[];
  // Some endpoints also return this; keep optional so partial loads work.
  campaignMapGalleryIndexId?: number;
  // Optional for callers that care about observer metadata.
  observers?: PlayerCampaignMapObserverResultDto[];
}

function normalizeObservedStructure(structure: ObservedStructureWireDto): ObservedStructureDto {
  return {
    id: structure.id ?? structure.Id ?? 0,
    structureId: structure.structureId ?? structure.StructureId ?? 0,
    positionOnTile: structure.positionOnTile ?? structure.PositionOnTile ?? 0,
    completionPercent: structure.completionPercent ?? structure.CompletionPercent ?? 0,
  };
}

function normalizeObservedTroop(troop: ObservedTroopWireDto): ObservedTroopDto {
  return {
    id: troop.id ?? troop.Id ?? 0,
    recordType: troop.recordType ?? troop.RecordType ?? 0,
    unitDesignId: troop.unitDesignId ?? troop.UnitDesignId ?? 0,
  };
}

function normalizeCampaignMapTile(tile: CampaignMapTileWireDto): CampaignMapTileDto {
  const structures = (tile.structures ?? tile.Structures ?? []).map(normalizeObservedStructure);
  const troops = (tile.troops ?? tile.Troops ?? []).map(normalizeObservedTroop);

  return {
    xCoord: tile.xCoord ?? tile.XCoord ?? 0,
    yCoord: tile.yCoord ?? tile.YCoord ?? 0,
    biomeTypeId: tile.biomeTypeId ?? tile.BiomeTypeId ?? 0,
    campaignMapGeographyTypeId: tile.campaignMapGeographyTypeId ?? tile.CampaignMapGeographyTypeId ?? 0,
    ...(structures.length > 0 ? { structures } : {}),
    ...(troops.length > 0 ? { troops } : {}),
  };
}

function normalizeObserver(observer: PlayerCampaignMapObserverResultWireDto): PlayerCampaignMapObserverResultDto {
  return {
    id: observer.id ?? observer.Id ?? observer.temporaryObserverId ?? observer.TemporaryObserverId ?? 0,
    observerRecordType: observer.observerRecordType ?? observer.ObserverRecordType ?? 0,
    xCoord: observer.xCoord ?? observer.XCoord ?? 0,
    yCoord: observer.yCoord ?? observer.YCoord ?? 0,
    seenTiles: (observer.seenTiles ?? observer.SeenTiles ?? []).map(normalizeCampaignMapTile),
  };
}

function normalizeResponse(data: PlayerCampaignMapResponseWireDto): PlayerCampaignMapResponseDto {
  return {
    observers: (data.observers ?? data.Observers ?? []).map(normalizeObserver),
    tiles: (data.tiles ?? data.Tiles ?? []).map(normalizeCampaignMapTile),
  };
}

function mergeTiles(tiles: CampaignMapTileDto[]): CampaignMapTileDto[] {
  const mergedByCoord = new Map<string, CampaignMapTileDto>();

  for (const tile of tiles) {
    const key = `${tile.xCoord},${tile.yCoord}`;
    const existing = mergedByCoord.get(key);

    if (!existing) {
      mergedByCoord.set(key, tile);
      continue;
    }

    mergedByCoord.set(key, {
      ...existing,
      ...tile,
      structures: tile.structures && tile.structures.length > 0 ? tile.structures : existing.structures,
      troops: tile.troops && tile.troops.length > 0 ? tile.troops : existing.troops,
    });
  }

  return [...mergedByCoord.values()];
}

function logTroopTable(response: PlayerCampaignMapResponseDto): void {
  const troopRows = response.observers.flatMap((observer) =>
    observer.seenTiles.flatMap((tile) =>
      (tile.troops ?? []).map((troop) => ({
        observerId: observer.id,
        observerRecordType: observer.observerRecordType,
        xCoord: tile.xCoord,
        yCoord: tile.yCoord,
        troopId: troop.id,
        troopRecordType: troop.recordType,
        unitDesignId: troop.unitDesignId,
      })),
    ),
  );

  console.log("[API] /campaign-map/player troop rows", troopRows.length);
  console.table(troopRows);
}

export async function postPlayerCampaignMap(req: LoadPlayerCampaignMapRequest): Promise<CampaignMapTilesResponse> {
  const baseUrl = http.defaults.baseURL ?? "";

  console.log("[API] POST /campaign-map/player", { baseUrl, req });

  const { data } = await http.post<PlayerCampaignMapResponseWireDto>("/campaign-map/player", req, {
    headers: { "Content-Type": "application/json" },
  });

  const normalizedResponse = normalizeResponse(data);
  logTroopTable(normalizedResponse);

  // Normalize to the flat tile list the rest of the UI expects.
  const tilesFromObservers = normalizedResponse.observers.flatMap((observer) => observer.seenTiles ?? []);
  const tiles = mergeTiles([...(normalizedResponse.tiles ?? []), ...tilesFromObservers]);

  return {
    tiles,
    observers: normalizedResponse.observers,
  };
}

export async function postClearPlayerCampaignMap(req: ClearPlayerCampaignMapRequest): Promise<void> {
  const baseUrl = http.defaults.baseURL ?? "";
  console.log("[API] POST /campaign-map/player/clear", { baseUrl, req });

  await http.post("/campaign-map/player/clear", req, {
    headers: { "Content-Type": "application/json" },
  });
}
