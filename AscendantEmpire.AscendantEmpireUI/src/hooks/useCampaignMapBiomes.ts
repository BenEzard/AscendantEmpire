import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { Biome } from "@/types/map/Biome";
import { getCampaignMapBiomes, putCampaignMapBiomes } from "@/api/biomesApi";

export const campaignMapBiomesKey = ["biomes", "campaign-map"] as const;

export function useCampaignMapBiomesQuery() {
  return useQuery({
    queryKey: campaignMapBiomesKey,
    queryFn: getCampaignMapBiomes,
  });
}

export function useSaveCampaignMapBiomesMutation(opts?: {
  onSuccess?: () => void;
  onError?: (error: unknown) => void;
}) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (biomes: Biome[]) => putCampaignMapBiomes(biomes),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: campaignMapBiomesKey });
      opts?.onSuccess?.();
    },
    onError: (error) => opts?.onError?.(error),
  });
}
