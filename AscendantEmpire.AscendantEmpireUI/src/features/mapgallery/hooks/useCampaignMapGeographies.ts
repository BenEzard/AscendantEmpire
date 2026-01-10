import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { Geography } from "../../../types/map/Geography";
import { getCampaignMapGeographies, putCampaignMapGeographies } from "../api/geographiesApi";

export const campaignMapGeographiesKey = ["geographies", "campaign-map"] as const;

export function useCampaignMapGeographiesQuery() {
  return useQuery({
    queryKey: campaignMapGeographiesKey,
    queryFn: getCampaignMapGeographies,
  });
}

export function useSaveCampaignMapGeographiesMutation(opts?: {
  onSuccess?: () => void;
  onError?: (error: unknown) => void;
}) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (geographies: Geography[]) => putCampaignMapGeographies(geographies),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: campaignMapGeographiesKey });
      opts?.onSuccess?.();
    },
    onError: (error) => opts?.onError?.(error),
  });
}
