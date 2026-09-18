export type Biome = {
  biomeType: number; // Unique identifier for this Biome
  name: string; // User-friendly name of this Biome
  movementCost: number; // The movement cost to traverse a Tile of this Biome
  doesBlockSight: boolean; // Does this Biome block Sight? (prevent a Unit/Structure from seeing through, or out of, it)
  isWater: boolean; // Is this Biome a water tile? (i.e. Ocean, Lake, River, etc.)
  isTraversable: boolean; // Is this Biome traversable? (i.e. can a Unit move through it)
  regenerationPercent: number | null; // Share of each sustenance Base that regrows every Round, as a fraction (0.25 = 25%); null when the Biome has no sustenance
  isPaintableOnCampaignMap: boolean; // Can this Biome be painted on a Campaign Map (by a map designer)?
  mapEditorHtmlColor: string; // HTML color string which this Biome will be shown as in the Map Editor
  canBeElevated: boolean; // Can this Biome be elevated? (i.e. can it be Hilly or Mountainous)

  // Regenerating Resources
  baseForageValue: number | null; // Base value of the Forage resource in this Biome
  baseHuntValue: number | null; // Base value of the Hunt resource in this Biome
  baseFishValue: number | null; // Base value of the Fish resource in this Biome
  baseFodderValue: number | null; // Base value of the Fodder resource in this Biome

  // Passive Material Values
  baseBoneValue: number | null; // Base value of the Bone resource in this Biome
  baseWoodValue: number | null; // Base value of the Wood resource in this Biome
  baseStoneValue: number | null; // Base value of the Stone resource in this Biome
};