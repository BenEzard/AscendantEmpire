import { http } from "@/api/http";

// gameId/playerId are client-side only: they scope the React Query cache, and are deliberately NOT sent to the server.
// The API reads both from the game token, so a request cannot ask about — or clear — another player's map.
export type LoadPlayerCampaignMapRequest = {
  gameId: number;
  playerId: number;
  loadWholeMap?: boolean;
};

export type ClearPlayerCampaignMapRequest = {
  gameId: number;
  playerId: number;
};

export interface CampaignMapTileDto {
  xCoord: number;
  yCoord: number;
  biomeTypeId: number;
  campaignMapGeographyTypeId: number;
  structures?: ObservedStructureDto[];
  forces?: ObservedForceDto[];
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
  ownerPlayerId: number;
  structureId: number;
  positionOnTile: number;
  completionPercent: number;
};

export type ObservedForceDto = {
  id: number;
  recordType: number;
  ownerPlayerId: number;
  healthyTroops: number;
  injuredTroops: number;
  initialStrength: number;
  unitTemplateId: number;
};

type ObservedStructureWireDto = Partial<{
  id: number;
  recordType: number;
  ownerPlayerId: number;
  structureId: number;
  positionOnTile: number;
  completionPercent: number;
}>;

type ObservedForceWireDto = Partial<{
  id: number;
  recordType: number;
  ownerPlayerId: number;
  unitTemplateId: number;
  healthyTroops: number;
  injuredTroops: number;
  initialStrength: number;
}>;

type CampaignMapTileWireDto = Partial<{
  xCoord: number;
  yCoord: number;
  biomeTypeId: number;
  campaignMapGeographyTypeId: number;
  structures: ObservedStructureWireDto[];
  forces: ObservedForceWireDto[];
}>;

type PlayerCampaignMapObserverResultWireDto = Partial<{
  id: number;
  observerRecordType: number;
  xCoord: number;
  yCoord: number;
  seenTiles: CampaignMapTileWireDto[];
  temporaryObserverId: number;
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
  tiles: CampaignMapTileWireDto[];
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
    id: structure.id ?? 0,
    ownerPlayerId: structure.ownerPlayerId ?? 0,
    structureId: structure.structureId ?? 0,
    positionOnTile: structure.positionOnTile ?? 0,
    completionPercent: structure.completionPercent ?? 0,
  };
}

function normalizeObservedForce(force: ObservedForceWireDto): ObservedForceDto {
  return {
    id: force.id ?? 0,
    recordType: force.recordType ?? 0,
    ownerPlayerId: force.ownerPlayerId ?? 0,
    unitTemplateId: force.unitTemplateId ?? 0,
    healthyTroops: force.healthyTroops ?? 0,
    injuredTroops: force.injuredTroops ?? 0,
    initialStrength: force.initialStrength ?? 0,
  };
}

function normalizeCampaignMapTile(tile: CampaignMapTileWireDto): CampaignMapTileDto {
  const structures = (tile.structures ?? []).map(normalizeObservedStructure);
  const forces = (tile.forces ?? []).map(normalizeObservedForce);

  return {
    xCoord: tile.xCoord ?? 0,
    yCoord: tile.yCoord ?? 0,
    biomeTypeId: tile.biomeTypeId ?? 0,
    campaignMapGeographyTypeId: tile.campaignMapGeographyTypeId ?? 0,
    ...(structures.length > 0 ? { structures } : {}),
    ...(forces.length > 0 ? { forces } : {}),
  };
}

function normalizeObserver(observer: PlayerCampaignMapObserverResultWireDto): PlayerCampaignMapObserverResultDto {
  return {
    id: observer.id ?? observer.temporaryObserverId ?? 0,
    observerRecordType: observer.observerRecordType ?? 0,
    xCoord: observer.xCoord ?? 0,
    yCoord: observer.yCoord ?? 0,
    seenTiles: (observer.seenTiles ?? []).map(normalizeCampaignMapTile),
  };
}

function normalizeResponse(data: PlayerCampaignMapResponseWireDto): PlayerCampaignMapResponseDto {
  return {
    observers: (data.observers ?? []).map(normalizeObserver),
    tiles: (data.tiles ?? []).map(normalizeCampaignMapTile),
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
      forces: tile.forces && tile.forces.length > 0 ? tile.forces : existing.forces,
    });
  }

  return [...mergedByCoord.values()];
}

function logForceTable(response: PlayerCampaignMapResponseDto): void {
  const forceRows = response.observers.flatMap((observer) =>
    observer.seenTiles.flatMap((tile) =>
      (tile.forces ?? []).map((force) => ({
        observerId: observer.id,
        observerRecordType: observer.observerRecordType,
        xCoord: tile.xCoord,
        yCoord: tile.yCoord,
        forceId: force.id,
        forceRecordType: force.recordType,
        unitTemplateId: force.unitTemplateId,
        healthyTroops: force.healthyTroops,
        injuredTroops: force.injuredTroops,
        initialStrength: force.initialStrength,
      })),
    ),
  );

  console.log("[API] /campaign-map/player force rows", forceRows.length);
  console.table(forceRows);
}

export async function postPlayerCampaignMap(req: LoadPlayerCampaignMapRequest): Promise<CampaignMapTilesResponse> {
  const baseUrl = http.defaults.baseURL ?? "";

  console.log("[API] POST /campaign-map/player", { baseUrl, req });

  // Only loadWholeMap goes over the wire — the server takes the game and player from the token.
  const { data } = await http.post<PlayerCampaignMapResponseWireDto>(
    "/campaign-map/player",
    { loadWholeMap: req.loadWholeMap ?? false },
    { headers: { "Content-Type": "application/json" } },
  );

  const normalizedResponse = normalizeResponse(data);
  logForceTable(normalizedResponse);

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

  // No body: the server clears the caller's own map, identified by the token.
  await http.post("/campaign-map/player/clear");
}
