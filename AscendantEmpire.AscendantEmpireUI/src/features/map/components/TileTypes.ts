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
};

export type TileCell = {
  x: number;
  y: number;

  biomeTypeId: number | null;
  geographyTypeId: number | null;
  structures?: ObservedStructureTile[];
  forces?: ObservedForceTile[];
};
