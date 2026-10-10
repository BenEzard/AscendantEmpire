import type { RiverWidth } from "@/types/map/Rivers";
import type { BiomeType } from "@/types/map/Biome";
import type { CampaignMapGeographyType } from "@/types/map/Geography";

export type TileCell = {
  x: number;
  y: number;

  biomeTypeId: BiomeType | null;
  geographyTypeId: CampaignMapGeographyType | null;

  // The two river edges this tile owns, exactly as the gallery map stores them: its South edge is the one it shares with the tile
  // below, its East edge the one it shares with the tile to its right.
  //
  // Raw rather than the four resolved edges the in-game map is sent, because these two columns are what the editor writes. A tile's
  // north and west edges belong to its neighbours, and a click on one is translated to that neighbour before it is saved — see
  // tools/riverEdgePicking.
  southRiver: RiverWidth;
  eastRiver: RiverWidth;
};
