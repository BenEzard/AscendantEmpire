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

  // Whether the force can still move this turn. The API masks this for forces the player doesn't own, and the
  // normalizer turns that absence into true — nothing we can't see the movement of should be shown as spent.
  hasMovementRemaining: boolean;

  // How much movement is left, and out of how much. Null for forces we don't own, where the API masks both rather
  // than telling us how far an enemy could still travel. Remaining can be negative: entering a tile takes half its
  // movement cost but spends all of it, so a slow force on rough ground finishes the turn owing points.
  movementPointsRemaining: number | null;
  campaignMovementPoints: number | null;
};

export type TileCell = {
  x: number;
  y: number;

  biomeTypeId: number | null;
  geographyTypeId: number | null;
  structures?: ObservedStructureTile[];
  forces?: ObservedForceTile[];
};
