import type { Biome } from "../types/map/Biome";
import { http } from "./http";

export async function getCampaignMapBiomes(): Promise<Biome[]> {
  const { data } = await http.get<{ biomes: Biome[] }>("/biomes/campaign-map");
  return data.biomes;
}

export async function putCampaignMapBiomes(biomes: Biome[]): Promise<void> {
  await http.put("/biomes/campaign-map", biomes, {
    headers: { "Content-Type": "application/json" },
  });
}
