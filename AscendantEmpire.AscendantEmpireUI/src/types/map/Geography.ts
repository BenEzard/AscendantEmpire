export type Geography = {
  geographyType: number; // Unique identifier for this Geography
  name: string; // User-friendly name of this Geography
  relativeElevation: number; // The relative elevation of this Geography
  movementCost: number; // The movement cost to traverse a Tile of this Geography
  mapEditorIcon?: string | null; // Icon used in the MapEditor to show the Geography.
  isPaintableOnCampaignMap: boolean; // Can this Biome be painted on a Campaign Map (by a map designer)?
};