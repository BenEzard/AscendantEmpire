import type { Geography } from "../types/map/Geography";
import { http } from "./http";

export async function getCampaignMapGeographies(): Promise<Geography[]> {
  const { data } = await http.get<{ geographies: Geography[] }>("/geography/campaign-map");
  return data.geographies;
}

export async function putCampaignMapGeographies(geographies: Geography[]): Promise<void> {
  await http.put("/geography/campaign-map", geographies, {
    headers: { "Content-Type": "application/json" },
  });
}
