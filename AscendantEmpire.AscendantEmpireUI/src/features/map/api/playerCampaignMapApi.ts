import { http } from "@/api/http";
import { RIVER_EDGES, type RiverWidth, type TileRivers } from "@/types/map/Rivers";
import type { TileOwnership, TileOwnershipStatus } from "@/types/map/Ownership";
import type { BiomeType } from "@/types/map/Biome";
import type { CampaignMapGeographyType } from "@/types/map/Geography";

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
  biomeTypeId: BiomeType;
  campaignMapGeographyTypeId: CampaignMapGeographyType;
  // Whether the player has ever seen this tile, and so knows the ground on it. The permanent half of the fog of war, and a
  // different question from whether anything is watching the tile right now — which is answered by the observers, not here.
  // Tiles reported by a real observer are always explored; only the whole-map read ever sends this false. See docs/fog-of-war.md.
  isExplored: boolean;
  // The river on each of the tile's four edges, or undefined for ground the player has never explored — which reports no terrain
  // at all. A tile that is explored and simply has no river arrives with all four edges "None", which is a different answer.
  //
  // Terrain, so it is sent for any explored tile whether or not something is watching it. Both banks report the edge they share;
  // drawing each edge once is RiverOverlay's job.
  rivers?: TileRivers;
  // Who holds the tile, as this player sees it, or undefined for ground no observer of theirs can see. Unlike the rivers above,
  // a border is current information rather than terrain, so the API reports it under live sight only — remembered ground sends
  // none at all rather than whatever it last was. A watched tile nobody holds arrives as "Unclaimed", which is a real answer.
  ownership?: TileOwnership;
  // Always present, never omitted when empty. An empty array is the API telling us there is nothing on this tile, which is
  // different from not having heard about the tile at all — and the merges below rely on being able to tell those apart, or a
  // force that moved away would keep being drawn on the tile it left.
  structures: ObservedStructureDto[];
  forces: ObservedForceDto[];
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
  hasCampaignMovementRemaining: boolean;
  // Null when the API masked it — we only get numbers for our own forces. Can be negative: a force holding any movement at
  // all may enter a tile and pays its whole cost, so a slow force crossing rough ground finishes the turn in debt. Can also
  // exceed the allowance, because movement left unspent is carried into the next turn on top of it.
  campaignMovementRemaining: number | null;
  campaignMovementPoints: number | null;
  // A separate pool from the campaign figures, masked the same way. Nothing spends or replenishes it yet.
  battleMovementRemaining: number | null;
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
  // Nullable on the wire: the API masks it for forces the player doesn't own, rather than telling them how much
  // movement an enemy has left. The two numbers below are masked by the same rule, and more sharply — they say
  // exactly how far a force could still go.
  hasCampaignMovementRemaining: boolean | null;
  campaignMovementRemaining: number | null;
  campaignMovementPoints: number | null;
  battleMovementRemaining: number | null;
}>;

type CampaignMapTileWireDto = Partial<{
  xCoord: number;
  yCoord: number;
  biomeTypeId: BiomeType;
  campaignMapGeographyTypeId: CampaignMapGeographyType;
  isExplored: boolean;
  // Null on the wire for an unexplored tile, and each edge is a RiverWidth member name. Partial rather than a full TileRivers,
  // because an older API sends nothing at all and a normalizer that assumed four edges would read undefined off it.
  rivers: Partial<TileRivers> | null;
  // Null on the wire for ground nothing is watching, and partial because an older API sends nothing at all.
  ownership: Partial<TileOwnership> | null;
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
    // Defaults to true, unlike the zero-defaults above. A masked (or simply absent) value means "we aren't told",
    // and the UI fades a force that has spent its movement — so guessing false would fade forces on no evidence.
    hasCampaignMovementRemaining: force.hasCampaignMovementRemaining ?? true,
    // Null rather than 0, for the same "we aren't told" reason — and here there is no safe guess available: 0 would read as a
    // spent force, and any other number would be a fabrication. The UI shows nothing at all when these are null.
    campaignMovementRemaining: force.campaignMovementRemaining ?? null,
    campaignMovementPoints: force.campaignMovementPoints ?? null,
    battleMovementRemaining: force.battleMovementRemaining ?? null,
  };
}

/**
 * Fills in the four edges of a tile's rivers, or gives back undefined when the API sent none.
 *
 * Undefined is preserved rather than flattened to an all-`"None"` object: no rivers at all is what an *unexplored* tile sends, and
 * turning that into "we know there is no river here" would put a claim about the ground behind the fog.
 */
function normalizeTileRivers(rivers: Partial<TileRivers> | null | undefined): TileRivers | undefined {
  if (rivers == null) {
    return undefined;
  }

  // Each edge defaults to "None": an edge the API left out is an edge with no river, which is the one safe reading — the map only
  // ever omits a width it has nothing to say about.
  return Object.fromEntries(RIVER_EDGES.map((edge) => [edge, rivers[edge] ?? ("None" as RiverWidth)])) as TileRivers;
}

/**
 * Fills in a tile's ownership, or gives back undefined when the API sent none.
 *
 * Undefined is preserved rather than flattened to an unowned record, for the same reason the rivers above preserve it: nothing
 * being reported is what *unwatched* ground sends, and turning that into "nobody holds this tile" would put a claim about the
 * current border behind the fog. A tile under live sight that genuinely has no holder arrives as a real `"Unclaimed"` record.
 */
function normalizeTileOwnership(ownership: Partial<TileOwnership> | null | undefined): TileOwnership | undefined {
  if (ownership == null) {
    return undefined;
  }

  return {
    // "Unknown" is the enum's own zero member and the honest reading of a record that named no status. It draws no border and
    // gets no tooltip line, so an API that sends a half-filled record says nothing rather than something wrong.
    status: ownership.status ?? ("Unknown" as TileOwnershipStatus),
    ownedByPlayerId: ownership.ownedByPlayerId ?? null,
    occupiedByPlayerId: ownership.occupiedByPlayerId ?? null,
    lastOwnedByPlayerId: ownership.lastOwnedByPlayerId ?? null,
    // Re-derived locally when absent rather than defaulted to null, so the fallback cannot be lost on the way across: the
    // occupier holds the tile if there is one, otherwise the owner.
    effectiveHolderPlayerId: ownership.effectiveHolderPlayerId ?? ownership.occupiedByPlayerId ?? ownership.ownedByPlayerId ?? null,
    // Defaults to empty, unlike the movement flag above: a claim we were not told about is one we must not draw, and the
    // neutral reading of silence here is "nobody claims this" rather than "somebody does, unnamed".
    //
    // Copied rather than passed through, so the drawing order the server chose cannot be disturbed by anything downstream
    // sorting the array in place. The border alternates the claimants' colours in this order and the tooltip lists them to match.
    claimedByPlayerIds: [...(ownership.claimedByPlayerIds ?? [])],
    isRebelling: ownership.isRebelling ?? false,
  };
}

export function normalizeCampaignMapTile(tile: CampaignMapTileWireDto): CampaignMapTileDto {
  return {
    xCoord: tile.xCoord ?? 0,
    yCoord: tile.yCoord ?? 0,
    // "Unknown" is the enums' own zero member, and the honest reading of a tile the API said nothing about. It matches no
    // paintable Biome or Geography, so the lookups fall through to their "(unknown ...)" labels.
    biomeTypeId: tile.biomeTypeId ?? "Unknown",
    campaignMapGeographyTypeId: tile.campaignMapGeographyTypeId ?? "Unknown",
    // Defaults to unexplored. An absent flag means the API told us nothing, and guessing "explored" would strip the fog off
    // ground the player has never been to — the one failure mode this flag exists to prevent.
    isExplored: tile.isExplored ?? false,
    rivers: normalizeTileRivers(tile.rivers),
    ownership: normalizeTileOwnership(tile.ownership),
    // Always emitted, even when empty. These used to be dropped when empty, which made "this tile is clear" indistinguishable
    // from "this tile wasn't reported" — and the merges then kept whatever was there before, so a force that stepped away would
    // stay drawn on the tile it left.
    structures: (tile.structures ?? []).map(normalizeObservedStructure),
    forces: (tile.forces ?? []).map(normalizeObservedForce),
  };
}

export function normalizeObserver(observer: PlayerCampaignMapObserverResultWireDto): PlayerCampaignMapObserverResultDto {
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

/**
 * Collapses a flat list of observed tiles so each coordinate appears once, merging what several observers saw of the same tile.
 *
 * Exported because a force step returns observers in the same shape and needs the same de-duplication before its tiles are
 * merged into the cache.
 */
export function mergeTilesByCoord(tiles: CampaignMapTileDto[]): CampaignMapTileDto[] {
  const mergedByCoord = new Map<string, CampaignMapTileDto>();

  for (const tile of tiles) {
    const key = `${tile.xCoord},${tile.yCoord}`;
    const existing = mergedByCoord.get(key);

    if (!existing) {
      mergedByCoord.set(key, tile);
      continue;
    }

    // `??`, not a length check: an empty array is a real answer meaning the tile is clear, and it has to win over what we held
    // before. Only a genuinely absent array falls back to the existing contents.
    mergedByCoord.set(key, {
      ...existing,
      ...tile,
      // `??` again, but for the opposite reason: rivers are terrain and never change, so an observer that reported none has
      // nothing to say about them and must not blank what another observer told us about the same tile.
      rivers: tile.rivers ?? existing.rivers,
      // `??` like the rivers, and not because ownership never changes — it does. An observer that reported none is simply not
      // watching this tile, and must not blank what the observer who *is* watching it told us. A border held over from a tile
      // that has since left sight is never drawn anyway: OwnershipOverlay gates on live sight before it draws one.
      ownership: tile.ownership ?? existing.ownership,
      structures: tile.structures ?? existing.structures,
      forces: tile.forces ?? existing.forces,
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
        hasCampaignMovementRemaining: force.hasCampaignMovementRemaining,
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
  const tiles = mergeTilesByCoord([...(normalizedResponse.tiles ?? []), ...tilesFromObservers]);

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
