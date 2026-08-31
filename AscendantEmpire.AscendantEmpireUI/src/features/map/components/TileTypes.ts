export type ObservedStructureTile = {
  id: number;
  structureId: number;
  positionOnTile: number;
  completionPercent: number;
};

export type ObservedTroopTile = {
  id: number;
  recordType: number;
  ownerPlayerId: number;
  unitTemplateId: number;
  healthyTroops: number;
  injuredTroops: number;
  initialStrength: number;
};

export type TileCell = {
  x: number;
  y: number;

  biomeTypeId: number | null;
  geographyTypeId: number | null;
  structures?: ObservedStructureTile[];
  troops?: ObservedTroopTile[];
};
