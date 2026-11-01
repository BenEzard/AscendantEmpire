/**
 * Which Geography this is — the C# `CampaignMapGeographyType` enum's member names, since the API serialises enums as their name rather than
 * their number (AscendantEmpire.Api/Program.cs registers a JsonStringEnumConverter). Same convention as BiomeType in ./Biome.ts.
 *
 * The server's numeric values (which have an intentional gap) never reach the client, so these are opaque names here. Elevation ordering comes
 * from each Geography's own RelativeElevation, not from these.
 */
export type CampaignMapGeographyType = "Unknown" | "DeepWater" | "ShallowWater" | "Flat" | "Hilly" | "Mountainous";

export type Geography = {
  campaignMapGeographyType: CampaignMapGeographyType; // Unique identifier for this Geography
  name: string; // User-friendly name of this Geography
  relativeElevation: number; // The relative elevation of this Geography
  movementCost: number; // The movement cost to traverse a Tile of this Geography
  isWater: boolean; // Is this Geography water? (i.e. Deep Water, Shallow Water)
  isTraversable: boolean; // Can anything enter this Geography at all? False for Mountainous
  mapEditorIcon?: string | null; // Icon used in the MapEditor to show the Geography.
  isPaintableOnCampaignMap: boolean; // Can this Biome be painted on a Campaign Map (by a map designer)?
};