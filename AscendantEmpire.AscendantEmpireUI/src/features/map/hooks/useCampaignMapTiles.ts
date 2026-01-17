import { useQuery } from "@tanstack/react-query";
import axios from "axios";

type LoadPlayerCampaignMapRequest = {
  gameGuid: string;
  playerGuid: string;
  loadWholeMap?: boolean;
};

export interface CampaignMapTileDto {
  campaignMapGalleryIndexId: number;
  xCoord: number;
  yCoord: number;
  biomeTypeId: number;
  campaignMapGeographyTypeId: number;
}

export interface CampaignMapTilesResponse {
  campaignMapGalleryIndexId: number;
  tiles: CampaignMapTileDto[];
}

const API_BASE = "https://localhost:7242";

export function usePlayerCampaignMapTilesQuery(gameGuid: string | undefined, playerGuid: string | undefined, enabled: boolean) {
  return useQuery<CampaignMapTilesResponse, Error>({
    queryKey: ["playerCampaignTiles", gameGuid, playerGuid],
    enabled:enabled && !!gameGuid && !!playerGuid,
    refetchOnWindowFocus: false,
    queryFn: async () => {
      if (gameGuid == null) throw new Error("gameGuid is null");
      if (playerGuid == null) throw new Error("playerGuid is null");
      
      console.log("Fetching campaign map tiles for player", playerGuid, "in game", gameGuid);
      const payload: LoadPlayerCampaignMapRequest = { gameGuid, playerGuid, loadWholeMap: true };
      const res = await axios.post<CampaignMapTilesResponse>(`${API_BASE}/campaign-map/player`, payload, { headers: { "Content-Type": "application/json" } });
      
      return res.data;
    },
  });
}
