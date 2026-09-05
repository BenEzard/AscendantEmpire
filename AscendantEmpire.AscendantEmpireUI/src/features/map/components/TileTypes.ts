export type ObservedStructureTile = {
  id: number;
  structureId: number;
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
  // Remaining can be negative: entering a tile takes half its movement cost but spends all of it, so a slow force on rough ground finishes the turn owing points. 
  // It can also exceed the allowance, because movement left unspent is carried into the next turn on top of it.
  campaignMovementRemaining: number | null;
  campaignMovementPoints: number | null;

  // Battle movement is a separate pool; masked the same way. Nothing spends or replenishes it yet.
  battleMovementRemaining: number | null;
};

export type TileCell = {
  x: number;
  y: number;

  biomeTypeId: number | null;
  geographyTypeId: number | null;

  // Whether the player has ever seen this tile. Carried on the cell rather than held in a separate set because it only ever
  // changes when the tile's DTO changes, so it rides along with the identity check that keeps memoised tiles from re-rendering.
  // Whether the tile is being watched *right now* is the other fog layer, and that one does live in a set — see useFogOfWar.
  isExplored: boolean;

  structures?: ObservedStructureTile[];
  forces?: ObservedForceTile[];
};
