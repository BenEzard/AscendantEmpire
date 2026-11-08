import type { TileRivers } from "@/types/map/Rivers";
import type { TileOwnership } from "@/types/map/Ownership";
import type { BiomeType } from "@/types/map/Biome";
import type { CampaignMapGeographyType } from "@/types/map/Geography";

export type ObservedStructureTile = {
  id: number;
  structureId: number;
  // The kind's catalogue code, which is what the artwork is keyed on. See structureImages.ts.
  structureCode: string;
  // The settlement standing here, or null when this structure is nobody's settlement.
  settlementName: string | null;
  positionOnTile: number;
  completionPercent: number;
};

export type ObservedForceTile = {
  id: number;
  recordType: number;
  ownerPlayerId: number;
  unitTemplateId: number;
  healthyTroops: number;
  injuredTroops: number;
  initialStrength: number;

  // Whether the force can still move this turn. 
  // The API masks this for forces the player doesn't own, and the normalizer turns that absence into true — nothing we can't see the movement of should be shown as spent.
  hasCampaignMovementRemaining: boolean;

  // How much movement is left, and out of how much. Null for forces we don't own, where the API masks both rather than telling us how far an enemy could still travel. 
  // Remaining can be negative: a force holding any movement at all may enter a tile and pays its whole cost, so a slow force on rough ground finishes the turn owing points.
  // It can also exceed the allowance, because movement left unspent is carried into the next turn on top of it.
  campaignMovementRemaining: number | null;
  campaignMovementPoints: number | null;

  // Battle movement is a separate pool; masked the same way. Nothing spends or replenishes it yet.
  battleMovementRemaining: number | null;
};

/**
 * A MapResource the player has discovered on a tile, as the tile draws it.
 *
 * Flattened from the whole-map read into exactly what one tile needs, so the grid can hand each memoised tile a stable array — see
 * groupMapResourcesByTile.
 */
export type TileMapResourceMarker = {
  /** Which of the tile's two places it takes, 1 or 2. Unique within a tile, so it both keys the marker and orders it. */
  slot: number;

  name: string;

  // Null for a type the biome already says, such as a forest's wood. No marker is drawn for those; the tile's hover title still names them, because the
  // player has discovered the resource and the title is where that knowledge is kept.
  iconPath: string | null;

  // The player's own estimate, in whole units, or null where there is no number to give: the tile is out of sight, or the resource holds its quantity in
  // layers to survey or in per-asset amounts. Never the true quantity, which the server does not send.
  estimatedQuantity: number | null;
};

export type TileCell = {
  x: number;
  y: number;

  biomeTypeId: BiomeType | null;
  geographyTypeId: CampaignMapGeographyType | null;

  // Whether the player has ever seen this tile. Carried on the cell rather than held in a separate set because it only ever
  // changes when the tile's DTO changes, so it rides along with the identity check that keeps memoised tiles from re-rendering.
  // Whether the tile is being watched *right now* is the other fog layer, and that one does live in a set — see useFogOfWar.
  isExplored: boolean;

  // The river on each of the tile's four edges, or undefined for ground the player has never explored — and for a grid with no
  // rivers at all, such as the map editor's. Rivers are terrain, so they stay on a tile that has fallen out of sight.
  //
  // Both banks report the edge they share. RiverOverlay is what draws each edge once.
  rivers?: TileRivers;

  // Who holds the tile, as this player sees it, or undefined for ground nothing of theirs is watching. Unlike the rivers above,
  // a border is current information rather than terrain, so the API reports it only under live sight and the overlay gates on
  // sight again before drawing — see OwnershipOverlay.
  //
  // Undefined on a grid with no ownership in it at all, such as the map editor's.
  ownership?: TileOwnership;

  structures?: ObservedStructureTile[];
  forces?: ObservedForceTile[];
};
