export type ObservedStructureTile = {
  id: number;
  structureId: number;
  positionOnTile: number;
  completionPercent: number;
};

export type TileCell = {
  x: number;
  y: number;

  biomeTypeId: number | null;
  geographyTypeId: number | null;
  structures?: ObservedStructureTile[];
};
